#import <BugSplat/BugSplat.h>
#import <UIKit/UIKit.h>

// Unity's root view controller, used to present the hang-test confirmation alert.
extern "C" UIViewController* UnityGetGLViewController(void);

#pragma clang diagnostic push
#pragma ide diagnostic ignored "OCUnusedGlobalDeclarationInspection"

extern "C" {
	NSString* createNSStringFrom(const char* cstring) {
		return [NSString stringWithUTF8String:(cstring ?: "")];
	}

	void _startBugSplat(const char* database, const char* application, const char* version) {
		BugSplat *bugsplat = [BugSplat shared];
		bugsplat.bugSplatDatabase = createNSStringFrom(database);
		bugsplat.applicationName = createNSStringFrom(application);
		bugsplat.applicationVersion = createNSStringFrom(version);
		bugsplat.autoSubmitCrashReport = YES;
		// Report fatal main-thread hangs. Coupled to iOS native crash reporting:
		// _startBugSplat is only invoked when UseNativeCrashReportingForIos is enabled.
		// Must be set before -start, which Unity calls on the main thread.
		bugsplat.enableHangDetection = YES;
		[bugsplat start];
	}

	void _setNativeAttributeIos(const char* key, const char* value) {
		[[BugSplat shared] setValue:createNSStringFrom(value) forAttribute:createNSStringFrom(key)];
	}

	void _setNativeUserIos(const char* user) {
		[BugSplat shared].userName = createNSStringFrom(user);
	}

	void _setNativeEmailIos(const char* email) {
		[BugSplat shared].userEmail = createNSStringFrom(email);
	}

	void _setNativeNotesIos(const char* notes) {
		[BugSplat shared].notes = createNSStringFrom(notes);
	}

	void _attachNativeLogFileIos(const char* path) {
		// Log file attachment is not supported on iOS because the BugSplatDelegate's
		// attachmentForBugSplat: method suppresses attributes from setValue:forAttribute:.
		// The Player.log is still uploaded via the managed .NET exception reporter.
	}

    void _crashNativeIos() {
        char *ptr = 0;
        *ptr += 1;
    }

    void _hangNativeIos() {
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
