#include <BugSplat/BugSplatManager.h>

NS_ASSUME_NONNULL_BEGIN

void Start() {
    [[BugsplatStartupManager sharedManager] start]; 
}

NS_ASSUME_NONNULL_END