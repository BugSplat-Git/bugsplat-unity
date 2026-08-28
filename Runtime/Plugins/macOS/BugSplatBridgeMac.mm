#import <Foundation/Foundation.h>
#import <dlfcn.h>
#import <objc/runtime.h>

static Class GetBugSplatClass() {
    static Class cls = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        cls = NSClassFromString(@"BugSplat");
        if (!cls) {
            NSString *frameworksPath = [[NSBundle mainBundle] privateFrameworksPath];
            NSString *plugInsPath = [[NSBundle mainBundle] builtInPlugInsPath];

            NSArray *searchPaths = @[
                [frameworksPath stringByAppendingPathComponent:@"BugSplat-macOS.dylib"],
                [plugInsPath stringByAppendingPathComponent:@"BugSplat-macOS.dylib"],
            ];

            for (NSString *path in searchPaths) {
                void *handle = dlopen([path UTF8String], RTLD_LAZY);
                if (handle) {
                    cls = NSClassFromString(@"BugSplat");
                    if (cls) break;
                }
            }

            if (!cls) {
                NSLog(@"BugSplat: Failed to load BugSplat-macOS.dylib: %s", dlerror());
            }
        }
    });
    return cls;
}

static id GetBugSplatInstance() {
    Class cls = GetBugSplatClass();
    if (!cls) return nil;
    return [cls performSelector:@selector(shared)];
}

// The array owns the tracked paths, so they stay alive whether or not this file is compiled with ARC.
static NSMutableArray<NSString *> *LogFilePaths() {
    static NSMutableArray<NSString *> *paths = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        paths = [[NSMutableArray alloc] init];
    });
    return paths;
}

static void AddLogFilePath(NSString *path) {
    if (path.length == 0) return;

    NSMutableArray<NSString *> *paths = LogFilePaths();
    @synchronized (paths) {
        if (![paths containsObject:path]) {
            [paths addObject:path];
        }
    }
}

static void RemoveLogFilePath(NSString *path) {
    if (path.length == 0) return;

    NSMutableArray<NSString *> *paths = LogFilePaths();
    @synchronized (paths) {
        [paths removeObject:path];
    }
}

static const unsigned long long kMaxLogAttachmentSizeBytes = 10ull * 1024ull * 1024ull;

static NSData *ReadLogTail(NSString *path) {
    NSFileHandle *fh = [NSFileHandle fileHandleForReadingAtPath:path];
    if (!fh) return nil;

    @try {
        unsigned long long fileSize = [fh seekToEndOfFile];
        unsigned long long readSize = MIN(fileSize, kMaxLogAttachmentSizeBytes);

        if (fileSize > readSize) {
            [fh seekToFileOffset:(fileSize - readSize)];
        } else {
            [fh seekToFileOffset:0];
        }

        NSData *data = [fh readDataToEndOfFile];
        [fh closeFile];
        return data;
    } @catch (__unused NSException *e) {
        [fh closeFile];
        return nil;
    }
}

static NSArray *DelegateAttachmentsForBugSplat(id self, SEL _cmd, id bugSplat) {
    Class attachmentClass = NSClassFromString(@"BugSplatAttachment");
    if (!attachmentClass) return @[];

    SEL initSel = @selector(initWithFilename:attachmentData:contentType:);
    NSMethodSignature *sig = [attachmentClass instanceMethodSignatureForSelector:initSel];
    if (!sig) return @[];

    NSMutableArray<NSString *> *tracked = LogFilePaths();
    NSArray<NSString *> *paths = nil;
    @synchronized (tracked) {
        paths = [NSArray arrayWithArray:tracked];
    }

    NSMutableArray *attachments = [NSMutableArray array];
    NSFileManager *fileManager = [NSFileManager defaultManager];

    for (NSString *path in paths) {
        // Existence is checked here rather than at attach time so a log file created after init still attaches.
        if (![fileManager fileExistsAtPath:path]) continue;

        NSData *data = ReadLogTail(path);
        if (!data || data.length == 0) continue;

        NSInvocation *inv = [NSInvocation invocationWithMethodSignature:sig];
        [inv setSelector:initSel];
        NSString *filename = [path lastPathComponent];
        NSString *contentType = @"text/plain";
        [inv setArgument:&filename atIndex:2];
        [inv setArgument:&data atIndex:3];
        [inv setArgument:&contentType atIndex:4];

        id rawAttachment = [attachmentClass alloc];
        [inv invokeWithTarget:rawAttachment];

        __unsafe_unretained id result = nil;
        [inv getReturnValue:&result];

        if (result) {
            [attachments addObject:result];
#if !__has_feature(objc_arc)
            [result release];
#endif
        }
    }

    return attachments;
}

// Delegate class built at runtime so the bridge does not have to link or import BugSplat's headers.
static id _delegateInstance = nil;

static id EnsureDelegate() {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        Class delegateClass = objc_allocateClassPair([NSObject class], "BugSplatUnityDelegate", 0);
        if (!delegateClass) return;

        Protocol *proto = NSProtocolFromString(@"BugSplatDelegate");
        if (proto) {
            class_addProtocol(delegateClass, proto);
        }
        class_addMethod(delegateClass, @selector(attachmentsForBugSplat:),
                        (IMP)DelegateAttachmentsForBugSplat, "@@:@");
        objc_registerClassPair(delegateClass);

        // BugSplat's delegate property is weak, so this instance is never released.
        _delegateInstance = [[delegateClass alloc] init];
    });
    return _delegateInstance;
}

extern "C" {
    void _startBugSplat(const char* database, const char* application, const char* version, const char* logFilePath,
                           int autoSubmitCrashReport, int autoSubmitFatalHangReport,
                           float hangDetectionThresholdSeconds) {
        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) {
            NSLog(@"BugSplat: BugSplat class not available");
            return;
        }

        NSString *db = [NSString stringWithUTF8String:(database ?: "")];
        NSString *app = [NSString stringWithUTF8String:(application ?: "")];
        NSString *ver = [NSString stringWithUTF8String:(version ?: "")];

        [bugsplat setValue:db forKey:@"bugSplatDatabase"];
        [bugsplat setValue:app forKey:@"applicationName"];
        [bugsplat setValue:ver forKey:@"applicationVersion"];

        // Report fatal main-thread hangs. Coupled to macOS native crash reporting:
        // _startBugSplat is only invoked when UseNativeCrashReportingForMac is enabled.
        // Must be set before -start, which Unity calls on the main thread — the tracker
        // captures the main thread's Mach port there. respondsToSelector first: setValue:forKey:
        // against an older BugSplat-macOS.dylib without the property would raise
        // NSUnknownKeyException rather than silently doing nothing.
        if ([bugsplat respondsToSelector:@selector(setEnableHangDetection:)]) {
            [bugsplat setValue:@YES forKey:@"enableHangDetection"];
            // Must be set before -start, same as the flags above: the tracker reads it at start.
            [bugsplat setValue:@(hangDetectionThresholdSeconds) forKey:@"hangDetectionThreshold"];
        }

        // Both of these are read while -start scans the previous session's pending reports, so they
        // have to be applied here rather than by a setter called after the constructor returns.
        [bugsplat setValue:(autoSubmitCrashReport ? @YES : @NO) forKey:@"autoSubmitCrashReport"];

        if ([bugsplat respondsToSelector:@selector(setAutoSubmitFatalHangReport:)]) {
            [bugsplat setValue:(autoSubmitFatalHangReport ? @YES : @NO)
                        forKey:@"autoSubmitFatalHangReport"];
        } else if (!autoSubmitFatalHangReport) {
            NSLog(@"BugSplat: this BugSplat-macOS.dylib predates autoSubmitFatalHangReport; "
                  @"fatal hang reports will continue to upload without asking.");
        }

        // Set up the attachment delegate BEFORE start so it's available when
        // pending crash reports are processed on launch.
        AddLogFilePath([NSString stringWithUTF8String:(logFilePath ?: "")]);
        [bugsplat setValue:EnsureDelegate() forKey:@"delegate"];

        [bugsplat performSelector:@selector(start)];
    }

    void _setNativeAttribute(const char* key, const char* value) {
        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;

        NSString *k = [NSString stringWithUTF8String:(key ?: "")];
        NSString *v = [NSString stringWithUTF8String:(value ?: "")];

        SEL sel = @selector(setValue:forAttribute:);
        if ([bugsplat respondsToSelector:sel]) {
            NSMethodSignature *sig = [bugsplat methodSignatureForSelector:sel];
            NSInvocation *inv = [NSInvocation invocationWithMethodSignature:sig];
            [inv setSelector:sel];
            [inv setArgument:&v atIndex:2];
            [inv setArgument:&k atIndex:3];
            [inv invokeWithTarget:bugsplat];
        }
    }

    void _setNativeUser(const char* user) {
        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;
        [bugsplat setValue:[NSString stringWithUTF8String:(user ?: "")] forKey:@"userName"];
    }

    void _setNativeEmail(const char* email) {
        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;
        [bugsplat setValue:[NSString stringWithUTF8String:(email ?: "")] forKey:@"userEmail"];
    }

    void _setNativeNotes(const char* notes) {
        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;
        [bugsplat setValue:[NSString stringWithUTF8String:(notes ?: "")] forKey:@"notes"];
    }

    void _setNativeKey(const char* key) {
        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;
        [bugsplat setValue:[NSString stringWithUTF8String:(key ?: "")] forKey:@"appKey"];
    }

    void _attachNativeLogFile(const char* path) {
        AddLogFilePath([NSString stringWithUTF8String:(path ?: "")]);

        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;

        [bugsplat setValue:EnsureDelegate() forKey:@"delegate"];
    }

    void _detachNativeLogFile(const char* path) {
        RemoveLogFilePath([NSString stringWithUTF8String:(path ?: "")]);
    }

    void _crashNative() {
        char *ptr = 0;
        *ptr += 1;
    }

    void _hangNative() {
        // Sample test hook: wedge the main thread. Unlike iOS, macOS has no watchdog that kills a
        // beachballing app, so a hang is only ever uploaded if the user force-quits it. The window
        // is about to stop drawing, so the instructions go to the log rather than on screen — and
        // deliberately not to an NSAlert, which would pull AppKit into a plugin that isn't linked
        // against it.
        NSLog(@"BugSplat: blocking the main thread indefinitely. To upload a hang report, force-quit "
              @"this app while it is frozen (Option-Command-Escape), then relaunch. Waiting the hang "
              @"out instead sends nothing - fatal hangs only.");

        // sleepUntilDate keeps the CPU idle while frozen (unlike a spin loop); the runloop still
        // stalls, so the hang tracker persists a report that uploads after a force-quit.
        [NSThread sleepUntilDate:[NSDate distantFuture]];
    }
}
