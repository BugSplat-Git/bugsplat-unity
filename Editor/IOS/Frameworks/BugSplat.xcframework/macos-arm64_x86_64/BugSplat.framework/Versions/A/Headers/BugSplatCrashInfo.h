//
//  BugSplatCrashInfo.h
//
//  Copyright © BugSplat, LLC. All rights reserved.
//

#import <Foundation/Foundation.h>

NS_ASSUME_NONNULL_BEGIN

/**
 * What kind of report is about to be sent.
 */
typedef NS_ENUM(NSInteger, BugSplatCrashInfoType) {
    /** A crash captured by the signal / Mach exception handler. */
    BugSplatCrashInfoTypeCrash = 0,
    /** A fatal main-thread hang captured by hang detection. */
    BugSplatCrashInfoTypeFatalHang
};

/**
 * Describes a persisted report that BugSplat is about to upload.
 *
 * Passed to `-bugSplat:shouldSendCrashReport:` so the report can be identified, counted or
 * logged before the decision to send it is made. Every property is read from the metadata
 * recorded alongside the report at the time it was captured, so the values describe the
 * session that crashed rather than the current one.
 *
 * Properties are nullable because a report may have been recorded by an SDK version that
 * predates them, or its metadata may be unreadable.
 */
@interface BugSplatCrashInfo : NSObject

/**
 * Whether this is a crash or a fatal hang.
 */
@property (nonatomic, readonly) BugSplatCrashInfoType type;

/**
 * The ID of the session the report was recorded in, or nil for reports that predate
 * session tracking.
 *
 * This is the value `BugSplat.sessionID` had during the session that crashed, which is
 * what ties the report to anything else recorded for that session.
 */
@property (nonatomic, readonly, copy, nullable) NSUUID *sessionID;

/**
 * When the report was captured, or nil if the timestamp is missing or unreadable.
 */
@property (nonatomic, readonly, copy, nullable) NSDate *crashDate;

/**
 * The application name recorded with the report.
 *
 * This is the name as of the crashed session, which can differ from the current
 * `BugSplat.applicationName` if the app was updated before the report was uploaded.
 */
@property (nonatomic, readonly, copy, nullable) NSString *applicationName;

/**
 * The application version recorded with the report, subject to the same caveat as
 * `applicationName`.
 */
@property (nonatomic, readonly, copy, nullable) NSString *applicationVersion;

/**
 * YES when the report is already marked to skip the crash dialog on its way out.
 *
 * This is the persisted "bypass the dialog" state, NOT proof of user consent. It is set
 * when the user agrees to send a report through the crash dialog, but also when a report
 * is marked for automatic submission - a fatal hang persisted while `autoSubmitFatalHangReport`
 * is enabled (the default) carries it without any dialog ever being shown, because the app
 * was frozen and the user never had the chance to consent.
 *
 * Treat it as "this report would be sent without asking", and do not present it to users as
 * something they agreed to.
 *
 * Returning NO from `-bugSplat:shouldSendCrashReport:` discards the report even when this
 * is YES, so check it if that prior decision should win.
 */
@property (nonatomic, readonly) BOOL userSubmitted;

/**
 * Designated initializer.
 */
- (instancetype)initWithType:(BugSplatCrashInfoType)type
                   sessionID:(nullable NSUUID *)sessionID
                   crashDate:(nullable NSDate *)crashDate
             applicationName:(nullable NSString *)applicationName
          applicationVersion:(nullable NSString *)applicationVersion
               userSubmitted:(BOOL)userSubmitted NS_DESIGNATED_INITIALIZER;

- (instancetype)init NS_UNAVAILABLE;
+ (instancetype)new NS_UNAVAILABLE;

@end

NS_ASSUME_NONNULL_END
