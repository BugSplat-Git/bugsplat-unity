#import <BugSplat/BugSplat.h>

#pragma clang diagnostic push
#pragma ide diagnostic ignored "OCUnusedGlobalDeclarationInspection"

static NSString *_logFilePath = nil;

@interface BugSplatUnityDelegate : NSObject <BugSplatDelegate>
@end

@implementation BugSplatUnityDelegate

- (BugSplatAttachment *)attachmentForBugSplat:(BugSplat *)bugSplat {
    if (!_logFilePath || ![[NSFileManager defaultManager] fileExistsAtPath:_logFilePath]) {
        return nil;
    }

    NSData *data = [NSData dataWithContentsOfFile:_logFilePath];
    if (!data) return nil;

    return [[BugSplatAttachment alloc] initWithFilename:@"Player.log"
                                        attachmentData:data
                                           contentType:@"text/plain"];
}

@end

static BugSplatUnityDelegate *_delegateInstance = nil;

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
		_logFilePath = createNSStringFrom(path);

		if (!_delegateInstance) {
			_delegateInstance = [[BugSplatUnityDelegate alloc] init];
		}
		[BugSplat shared].delegate = _delegateInstance;
	}

    void _crashNativeIos() {
        char *ptr = 0;
        *ptr += 1;
    }
}
