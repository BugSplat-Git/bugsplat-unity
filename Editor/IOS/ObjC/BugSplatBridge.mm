#import <BugSplat/BugSplat.h>
#import <UIKit/UIKit.h>

// Unity's root view controller, used to present the hang-test confirmation alert.
extern "C" UIViewController* UnityGetGLViewController(void);

#pragma clang diagnostic push
#pragma ide diagnostic ignored "OCUnusedGlobalDeclarationInspection"

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

@interface BugSplatUnityDelegate : NSObject <BugSplatDelegate>
@end

@implementation BugSplatUnityDelegate

- (NSArray<BugSplatAttachment *> *)attachmentsForBugSplat:(BugSplat *)bugSplat {
	NSMutableArray<NSString *> *tracked = LogFilePaths();
	NSArray<NSString *> *paths = nil;
	@synchronized (tracked) {
		paths = [NSArray arrayWithArray:tracked];
	}

	NSMutableArray<BugSplatAttachment *> *attachments = [NSMutableArray array];
	NSFileManager *fileManager = [NSFileManager defaultManager];

	for (NSString *path in paths) {
		// Existence is checked here rather than at attach time so a log file created after init still attaches.
		if (![fileManager fileExistsAtPath:path]) continue;

		NSData *data = ReadLogTail(path);
		if (!data || data.length == 0) continue;

		BugSplatAttachment *attachment = [[BugSplatAttachment alloc]
			initWithFilename:[path lastPathComponent]
			attachmentData:data
			contentType:@"text/plain"];
		[attachments addObject:attachment];
#if !__has_feature(objc_arc)
		[attachment release];
#endif
	}

	return attachments;
}

@end

static BugSplatUnityDelegate *_delegateInstance = nil;

static BugSplatUnityDelegate *EnsureDelegate() {
	static dispatch_once_t onceToken;
	dispatch_once(&onceToken, ^{
		// BugSplat's delegate property is weak, so this instance is never released.
		_delegateInstance = [[BugSplatUnityDelegate alloc] init];
	});
	return _delegateInstance;
}

extern "C" {
	NSString* createNSStringFrom(const char* cstring) {
		return [NSString stringWithUTF8String:(cstring ?: "")];
	}

	void _startBugSplat(const char* database, const char* application, const char* version,
	                    const char* logFilePath, int autoSubmitCrashReport, int autoSubmitFatalHangReport,
	                    float hangDetectionThresholdSeconds) {
		BugSplat *bugsplat = [BugSplat shared];
		bugsplat.bugSplatDatabase = createNSStringFrom(database);
		bugsplat.applicationName = createNSStringFrom(application);
		bugsplat.applicationVersion = createNSStringFrom(version);
		// Both are read while -start scans the previous session's pending reports, so they have to
		// be set here rather than by a setter the C# side calls once the constructor has returned.
		bugsplat.autoSubmitCrashReport = autoSubmitCrashReport ? YES : NO;
		// Set through KVC rather than the property. The vendored BugSplat.xcframework predates
		// autoSubmitFatalHangReport, and unlike the macOS bridge - which reaches BugSplat purely
		// at runtime - this file links the framework, so a direct property access would not
		// compile at all. respondsToSelector: keeps it a runtime no-op until a framework
		// carrying the property is vendored, and NSSelectorFromString avoids
		// -Wundeclared-selector on the same undeclared name.
		if ([bugsplat respondsToSelector:NSSelectorFromString(@"setAutoSubmitFatalHangReport:")]) {
			[bugsplat setValue:(autoSubmitFatalHangReport ? @YES : @NO)
			            forKey:@"autoSubmitFatalHangReport"];
		} else if (!autoSubmitFatalHangReport) {
			NSLog(@"BugSplat: this BugSplat.xcframework predates autoSubmitFatalHangReport; "
			      @"fatal hang reports will continue to upload without asking.");
		}
		// Report fatal main-thread hangs. Coupled to iOS native crash reporting:
		// _startBugSplat is only invoked when UseNativeCrashReportingForIos is enabled.
		// Must be set before -start, which Unity calls on the main thread.
		bugsplat.enableHangDetection = YES;
		// Must be set before -start, same as the flags above: the tracker reads it when it starts.
		bugsplat.hangDetectionThreshold = hangDetectionThresholdSeconds;
		// Track the log BEFORE start as well as the delegate. start processes the crash reports
		// left by the previous session and asks the delegate for attachments while it does, so a
		// path added afterwards arrives too late for the very reports it was meant to accompany.
		AddLogFilePath(createNSStringFrom(logFilePath));
		bugsplat.delegate = EnsureDelegate();
		[bugsplat start];
	}

	void _setNativeAttribute(const char* key, const char* value) {
		[[BugSplat shared] setValue:createNSStringFrom(value) forAttribute:createNSStringFrom(key)];
	}

	void _setNativeUser(const char* user) {
		[BugSplat shared].userName = createNSStringFrom(user);
	}

	void _setNativeEmail(const char* email) {
		[BugSplat shared].userEmail = createNSStringFrom(email);
	}

	void _setNativeNotes(const char* notes) {
		[BugSplat shared].notes = createNSStringFrom(notes);
	}

	void _setNativeKey(const char* key) {
		[BugSplat shared].appKey = createNSStringFrom(key);
	}

	void _attachNativeLogFile(const char* path) {
		AddLogFilePath(createNSStringFrom(path));
		[BugSplat shared].delegate = EnsureDelegate();
	}

	void _detachNativeLogFile(const char* path) {
		RemoveLogFilePath(createNSStringFrom(path));
	}

    void _crashNative() {
        char *ptr = 0;
        *ptr += 1;
    }

    void _hangNative() {
        // Sample test hook: confirm with an alert, then wedge the main thread.
        // A hang is only uploaded if the app is terminated while frozen, so the
        // alert tells the user to force-quit and relaunch to see the report.
        NSString *bundleId = [[NSBundle mainBundle] bundleIdentifier] ?: @"<bundle-id>";
        NSString *message = [NSString stringWithFormat:
            @"The main thread will be blocked indefinitely. To upload a hang report "
            @"you must force-quit the app while it's frozen — swipe up from the app "
            @"switcher on a device, or run `xcrun simctl terminate booted %@` on the "
            @"Simulator. The report uploads on the next launch. If you wait it out "
            @"instead, nothing is sent — fatal hangs only.", bundleId];

        UIAlertController *alert = [UIAlertController
            alertControllerWithTitle:@"Simulate Fatal Hang?"
                             message:message
                      preferredStyle:UIAlertControllerStyleAlert];

        [alert addAction:[UIAlertAction actionWithTitle:@"Cancel"
                                                  style:UIAlertActionStyleCancel
                                                handler:nil]];
        [alert addAction:[UIAlertAction actionWithTitle:@"Hang App"
                                                  style:UIAlertActionStyleDestructive
                                                handler:^(UIAlertAction *action) {
            // Block the main thread indefinitely. sleepUntilDate keeps the CPU idle
            // while frozen (unlike a spin loop); the runloop still stalls, so the
            // hang tracker persists a report that uploads after a force-quit.
            [NSThread sleepUntilDate:[NSDate distantFuture]];
        }]];

        [UnityGetGLViewController() presentViewController:alert animated:YES completion:nil];
    }
}
