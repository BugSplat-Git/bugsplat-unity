#import <BugSplat/BugSplat.h>

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
}
