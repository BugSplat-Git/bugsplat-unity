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

// Log file path stored for delegate callback
static NSString *_logFilePath = nil;

// Delegate class created at runtime to provide log file attachment
static Class _delegateClass = nil;

static NSArray *DelegateAttachmentsForBugSplat(id self, SEL _cmd, id bugSplat) {
    if (!_logFilePath || ![[NSFileManager defaultManager] fileExistsAtPath:_logFilePath]) {
        return @[];
    }

    NSData *data = [NSData dataWithContentsOfFile:_logFilePath];
    if (!data) return @[];

    Class attachmentClass = NSClassFromString(@"BugSplatAttachment");
    if (!attachmentClass) return @[];

    id attachment = [[attachmentClass alloc] performSelector:@selector(initWithFilename:attachmentData:contentType:)
                                                 withObject:@"Player.log"
                                                 withObject:data];
    // contentType doesn't get passed with performSelector:withObject:withObject:
    // Use NSInvocation instead
    SEL initSel = @selector(initWithFilename:attachmentData:contentType:);
    NSMethodSignature *sig = [attachmentClass instanceMethodSignatureForSelector:initSel];
    if (!sig) return @[];

    NSInvocation *inv = [NSInvocation invocationWithMethodSignature:sig];
    [inv setSelector:initSel];
    NSString *filename = @"Player.log";
    NSString *contentType = @"text/plain";
    [inv setArgument:&filename atIndex:2];
    [inv setArgument:&data atIndex:3];
    [inv setArgument:&contentType atIndex:4];

    id rawAttachment = [attachmentClass alloc];
    [inv invokeWithTarget:rawAttachment];

    __unsafe_unretained id result = nil;
    [inv getReturnValue:&result];

    return result ? @[result] : @[];
}

static void EnsureDelegateClass() {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        _delegateClass = objc_allocateClassPair([NSObject class], "BugSplatUnityDelegate", 0);
        if (_delegateClass) {
            Protocol *proto = NSProtocolFromString(@"BugSplatDelegate");
            if (proto) {
                class_addProtocol(_delegateClass, proto);
            }
            class_addMethod(_delegateClass, @selector(attachmentsForBugSplat:),
                           (IMP)DelegateAttachmentsForBugSplat, "@@:@");
            objc_registerClassPair(_delegateClass);
        }
    });
}

static id _delegateInstance = nil;

extern "C" {
    void _startBugSplatMac(const char* database, const char* application, const char* version, const char* logFilePath) {
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

        // Set up log file delegate BEFORE start so it's available when
        // pending crash reports are processed on launch
        NSString *path = [NSString stringWithUTF8String:(logFilePath ?: "")];
        if (path.length > 0 && [[NSFileManager defaultManager] fileExistsAtPath:path]) {
            _logFilePath = path;
            EnsureDelegateClass();
            if (!_delegateInstance) {
                _delegateInstance = [[_delegateClass alloc] init];
            }
            [bugsplat setValue:_delegateInstance forKey:@"delegate"];
            NSLog(@"BugSplat: Attached log file: %@", _logFilePath);
        }

        [bugsplat performSelector:@selector(start)];
    }

    void _setNativeAttributeMac(const char* key, const char* value) {
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

    void _setNativeUserMac(const char* user) {
        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;
        [bugsplat setValue:[NSString stringWithUTF8String:(user ?: "")] forKey:@"userName"];
    }

    void _setNativeEmailMac(const char* email) {
        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;
        [bugsplat setValue:[NSString stringWithUTF8String:(email ?: "")] forKey:@"userEmail"];
    }

    void _setNativeNotesMac(const char* notes) {
        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;
        [bugsplat setValue:[NSString stringWithUTF8String:(notes ?: "")] forKey:@"notes"];
    }

    void _attachNativeLogFileMac(const char* path) {
        _logFilePath = [NSString stringWithUTF8String:(path ?: "")];

        id bugsplat = GetBugSplatInstance();
        if (!bugsplat) return;

        EnsureDelegateClass();
        if (!_delegateInstance) {
            _delegateInstance = [[_delegateClass alloc] init];
        }
        [bugsplat setValue:_delegateInstance forKey:@"delegate"];
    }

    void _crashNativeMac() {
        char *ptr = 0;
        *ptr += 1;
    }
}
