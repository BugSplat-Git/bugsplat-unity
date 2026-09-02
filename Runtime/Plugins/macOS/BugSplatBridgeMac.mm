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

// The path Unity writes this session's Player.log to. Kept apart from the generic list because it
// is the one tracked path whose contents are wrong by the time the delegate runs: a macOS crash
// report uploads at the NEXT launch, and by then Unity has renamed the crashed session's log to
// Player-prev.log and started a fresh Player.log that knows nothing about the crash.
static NSString *_playerLogPath = nil;

// Unity keeps exactly one rotated log, as a sibling of Player.log.
static NSString *PreviousSessionPlayerLogPath(NSString *playerLogPath) {
    if (playerLogPath.length == 0) return nil;
    return [[playerLogPath stringByDeletingLastPathComponent] stringByAppendingPathComponent:@"Player-prev.log"];
}

// Each launch records which file Player.log was, keyed by session ID, so the delegate can tell
// whether Player-prev.log is the crashed session's log. Unity rotates by renaming, which keeps
// the inode and creation date, so the identity survives to the next launch. The case this
// catches: the app crashes again before BugSplat starts, so that session is never recorded and
// its rotation leaves a different file behind. Attaching that file would describe the wrong
// session; the check turns it into no attachment instead.
static NSString *const kSessionLogRecordKey = @"com.bugsplat.unity.sessionLog";
static NSString *const kRecordSessionID = @"sessionID";
static NSString *const kRecordInode = @"inode";
static NSString *const kRecordCreated = @"created";

// Written by the previous launch that reached this code. Loaded before -start, because -start is
// where the delegate runs, and this launch's own record must not replace it until afterwards.
static NSDictionary *_previousSessionLogRecord = nil;

static NSDictionary *IdentityOfFile(NSString *path) {
    if (path.length == 0) return nil;
    NSDictionary *attrs = [[NSFileManager defaultManager] attributesOfItemAtPath:path error:nil];
    NSNumber *inode = attrs[NSFileSystemFileNumber];
    NSDate *created = attrs[NSFileCreationDate];
    if (!inode || !created) return nil;
    return @{ kRecordInode: inode, kRecordCreated: @([created timeIntervalSince1970]) };
}

static BOOL IdentityMatches(NSDictionary *record, NSDictionary *identity) {
    if (!record || !identity) return NO;
    if (![record[kRecordInode] isEqual:identity[kRecordInode]]) return NO;
    // Sessions are launches apart; one second of slack covers any plist round-trip loss.
    double recorded = [record[kRecordCreated] doubleValue];
    double actual = [identity[kRecordCreated] doubleValue];
    return fabs(recorded - actual) < 1.0;
}

// YES when Player-prev.log is shown to be the crashed session's log, or when there is nothing to
// compare against (a report or a launch predating this check), where it is the best available
// guess and matches what every earlier version attached. NO only when the evidence says the file
// belongs to a different session.
static BOOL PreviousLogBelongsToSession(NSUUID *crashedSessionID, NSString *previousLogPath) {
    NSDictionary *record = _previousSessionLogRecord;
    if (!crashedSessionID || !record) return YES;

    if (![record[kRecordSessionID] isEqualToString:crashedSessionID.UUIDString]) return NO;
    return IdentityMatches(record, IdentityOfFile(previousLogPath));
}

static void RecordCurrentSessionLog(id bugsplat, NSString *playerLogPath) {
    NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];

    NSString *sessionID = nil;
    if ([bugsplat respondsToSelector:@selector(sessionID)]) {
        sessionID = [[bugsplat valueForKey:@"sessionID"] UUIDString];
    }
    NSDictionary *identity = IdentityOfFile(playerLogPath);

    // A record that cannot be completed is removed rather than left stale: no record means the
    // next launch attaches on best effort, which is better than comparing against the wrong launch.
    if (sessionID.length == 0 || !identity) {
        [defaults removeObjectForKey:kSessionLogRecordKey];
        return;
    }

    [defaults setObject:@{ kRecordSessionID: sessionID,
                           kRecordInode: identity[kRecordInode],
                           kRecordCreated: identity[kRecordCreated] }
                 forKey:kSessionLogRecordKey];
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

static NSArray *AttachmentsForCrashedSession(NSUUID *crashedSessionID) {
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

    for (NSString *trackedPath in paths) {
        NSString *path = trackedPath;
        // Reported under the tracked name: on the report this is the crashed session's Player.log,
        // and "-prev" only describes when it was read.
        NSString *filename = [trackedPath lastPathComponent];

        if (_playerLogPath && [trackedPath isEqualToString:_playerLogPath]) {
            // Read the crashed session's log, not the one this launch is writing.
            path = PreviousSessionPlayerLogPath(trackedPath);

            if (!PreviousLogBelongsToSession(crashedSessionID, path)) {
                NSLog(@"BugSplat: Player-prev.log is not the crashed session's log (the app was "
                      @"relaunched and crashed again before BugSplat started); omitting Player.log "
                      @"rather than attaching the wrong one.");
                continue;
            }
        }

        // Existence is checked here rather than at attach time so a log file created after init still attaches.
        if (![fileManager fileExistsAtPath:path]) continue;

        NSData *data = ReadLogTail(path);
        if (!data || data.length == 0) continue;

        NSInvocation *inv = [NSInvocation invocationWithMethodSignature:sig];
        [inv setSelector:initSel];
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

// bugsplat-apple prefers the sessionID variant when the delegate responds to it, and passes the
// ID of the session that crashed. The plain variant stays for dylibs that predate it.
static NSArray *DelegateAttachmentsForBugSplatSession(id self, SEL _cmd, id bugSplat, NSUUID *sessionID) {
    return AttachmentsForCrashedSession(sessionID);
}

static NSArray *DelegateAttachmentsForBugSplat(id self, SEL _cmd, id bugSplat) {
    return AttachmentsForCrashedSession(nil);
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
        class_addMethod(delegateClass, @selector(attachmentsForBugSplat:sessionID:),
                        (IMP)DelegateAttachmentsForBugSplatSession, "@@:@@");
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
            // Guarded separately from enableHangDetection - the two are different keys, and
            // setValue:forKey: on a key the dylib lacks raises NSUnknownKeyException rather than
            // failing quietly. They shipped together, but that is not something to rely on.
            if (hangDetectionThresholdSeconds > 0 &&
                [bugsplat respondsToSelector:@selector(setHangDetectionThreshold:)]) {
                [bugsplat setValue:@(hangDetectionThresholdSeconds) forKey:@"hangDetectionThreshold"];
            }
        }

        // Both of these are read while -start scans the previous session's pending reports, so they
        // have to be applied here rather than by a setter called after the constructor returns.
        // Negative means the caller expressed no preference, so leave bugsplat-apple's own
        // per-platform default in place rather than picking one for them.
        if (autoSubmitCrashReport >= 0) {
            [bugsplat setValue:(autoSubmitCrashReport ? @YES : @NO) forKey:@"autoSubmitCrashReport"];
        }

        if (autoSubmitFatalHangReport >= 0 &&
            [bugsplat respondsToSelector:@selector(setAutoSubmitFatalHangReport:)]) {
            [bugsplat setValue:(autoSubmitFatalHangReport ? @YES : @NO)
                        forKey:@"autoSubmitFatalHangReport"];
        } else if (autoSubmitFatalHangReport == 0) {
            NSLog(@"BugSplat: this BugSplat-macOS.dylib predates autoSubmitFatalHangReport; "
                  @"fatal hang reports will continue to upload without asking.");
        }

        // Set up the attachment delegate BEFORE start so it's available when
        // pending crash reports are processed on launch.
        NSString *playerLog = [NSString stringWithUTF8String:(logFilePath ?: "")];
        if (playerLog.length > 0) {
#if !__has_feature(objc_arc)
            [_playerLogPath release];
#endif
            _playerLogPath = [playerLog copy];
        }
        AddLogFilePath(playerLog);
        [bugsplat setValue:EnsureDelegate() forKey:@"delegate"];

        // The delegate runs inside -start and must compare against the previous launch's record.
#if !__has_feature(objc_arc)
        [_previousSessionLogRecord release];
#endif
        _previousSessionLogRecord =
            [[[NSUserDefaults standardUserDefaults] dictionaryForKey:kSessionLogRecordKey] copy];

        [bugsplat performSelector:@selector(start)];

        RecordCurrentSessionLog(bugsplat, playerLog);
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
