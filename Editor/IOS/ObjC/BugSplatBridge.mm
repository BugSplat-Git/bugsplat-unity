#import <Bugsplat/Bugsplat.h>

#pragma clang diagnostic push
#pragma ide diagnostic ignored "OCUnusedGlobalDeclarationInspection"

extern "C" {
	char* cStringCopy(const char* string) {
		char *res = (char *) malloc(strlen(string) + 1);
		strcpy(res, string);
		return res;
	}
	
	char* createCStringFrom(NSString* string) {
		if (!string) {
			string = @"";
		}
		
		return cStringCopy([string UTF8String]);
	}

	NSString* createNSStringFrom(const char* cstring) {
		return [NSString stringWithUTF8String:(cstring ?: "")];
	}

	void _startBugSplat() {
		[[BugsplatStartupManager sharedManager] start];
	}

    void _crashNativeIos() {
        char *ptr = 0;
        *ptr += 1;
    }

	char* _getBuildNumber() {
		return createCStringFrom([[NSBundle mainBundle] objectForInfoDictionaryKey:@"CFBundleVersion"]);
	}
}
