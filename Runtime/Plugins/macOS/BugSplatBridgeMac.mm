#import <Foundation/Foundation.h>
#import <dlfcn.h>

static Class GetBugSplatClass() {
    static Class cls = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        cls = NSClassFromString(@"BugSplat");
        if (!cls) {
            // Framework not loaded yet — try to dlopen from the app bundle
            NSString *frameworksPath = [[NSBundle mainBundle] privateFrameworksPath];
            NSString *plugInsPath = [[NSBundle mainBundle] builtInPlugInsPath];

            NSArray *searchPaths = @[
                [frameworksPath stringByAppendingPathComponent:@"BugSplat.dylib"],
                [plugInsPath stringByAppendingPathComponent:@"BugSplat.dylib"],
            ];

            for (NSString *path in searchPaths) {
                void *handle = dlopen([path UTF8String], RTLD_LAZY);
                if (handle) {
                    cls = NSClassFromString(@"BugSplat");
                    if (cls) break;
                }
            }

            if (!cls) {
                NSLog(@"BugSplat: Failed to load BugSplat.dylib: %s", dlerror());
            }
        }
    });
    return cls;
}

extern "C" {
    void _startBugSplatMac(const char* database, const char* application, const char* version) {
        Class bugSplatClass = GetBugSplatClass();
        if (!bugSplatClass) {
            NSLog(@"BugSplat: BugSplat class not available");
            return;
        }

        id bugsplat = [bugSplatClass performSelector:@selector(shared)];

        NSString *db = [NSString stringWithUTF8String:(database ?: "")];
        NSString *app = [NSString stringWithUTF8String:(application ?: "")];
        NSString *ver = [NSString stringWithUTF8String:(version ?: "")];

        [bugsplat setValue:db forKey:@"bugSplatDatabase"];
        [bugsplat setValue:app forKey:@"applicationName"];
        [bugsplat setValue:ver forKey:@"applicationVersion"];
        [bugsplat setValue:@YES forKey:@"autoSubmitCrashReport"];

        [bugsplat performSelector:@selector(start)];
    }

    void _crashNativeMac() {
        char *ptr = 0;
        *ptr += 1;
    }
}
