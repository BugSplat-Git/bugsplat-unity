//
//  BugSplat.h
//
//  Copyright © BugSplat, LLC. All rights reserved.
//

#import <TargetConditionals.h>
#import <Foundation/Foundation.h>

//! Project version number for BugSplat.
FOUNDATION_EXPORT double BugSplatVersionNumber;

//! Project version string for BugSplat.
FOUNDATION_EXPORT const unsigned char BugSplatVersionString[];

// In this header, you should import all the public headers of your framework using statements like #import <BugSplat/PublicHeader.h>

#import <BugSplat/BugSplatDelegate.h>
#import <BugSplat/BugSplatAttachment.h>
#import <BugSplat/BugSplatFeedbackResult.h>
#if TARGET_OS_OSX
#import <BugSplat/BugSplatMac.h>
#endif

NS_ASSUME_NONNULL_BEGIN

/// App's Info.plist String entry which is a customer specific BugSplat database name where crash reports will be uploaded.
/// e.g: "fred" (which will reference the https://fred.bugsplat.com/ database)
#define kBugSplatDatabase @"BugSplatDatabase"


@protocol BugSplatDelegate;

@interface BugSplat : NSObject

/*!
 *  BugSplat singleton initializer/accessor
 *
 *  @return shared instance of BugSplat
 */
+ (instancetype)shared;

/*!
 *  Configures and starts crash reporting service
 */
- (void)start;

/**
 * Set the delegate
 *
 * Defines the class that implements the optional protocol `BugSplatDelegate`.
 *
 * @see BugSplatDelegate
 */
@property (weak, nonatomic, nullable) id<BugSplatDelegate> delegate;

/**
 * A unique identifier for the current app session (process launch).
 *
 * A new value is generated every launch and remains stable for the lifetime
 * of the process. It is embedded into any crash report captured during this
 * session, which makes it the key for associating session-scoped data (such
 * as per-session log files) with the crash that ended the session.
 *
 * Recommended usage:
 * 1. `sessionID` is available as soon as the `BugSplat` instance is created
 *    (e.g. via `+[BugSplat shared]`) and does not change for the lifetime of the
 *    process — you do not need to wait for `start`. Read it and durably record a
 *    mapping from it to any session-scoped files you may want to attach to a crash
 *    report (e.g. this session's log file path). Use a per-session file name — a
 *    fixed path that is overwritten each launch cannot be recovered later.
 * 2. If the app crashes, the `BugSplatDelegate` callbacks for that crash
 *    (`attachmentsForBugSplat:sessionID:`, `bugSplatDidFinishSendingCrashReport:sessionID:`,
 *    etc.) are passed the **crashed** session's ID — not the current one — so you
 *    can look up the recorded mapping, return the right files, and clean up
 *    once the report has been sent.
 *
 * @note The session ID passed to delegate callbacks may be nil for crash
 * reports recorded by versions of BugSplat that predate this property.
 *
 * @see BugSplatDelegate
 */
@property (nonatomic, readonly) NSUUID *sessionID;

/**
 * The database name BugSplat will use to construct the BugSplatDatabase URL where crash reports will be submitted.
 *
 * By default, the BugSplat database name is pulled from the App's Info.plist (BugSplatDatabase key).
 *
 * Set this property to override the Info.plist value or to set the database programmatically.
 * The value can be changed at any time and will be captured at crash time.
 *
 * @note If neither Info.plist nor this property is set before calling `start`, an assertion will fail.
 */
@property (nonatomic, copy, nullable) NSString *bugSplatDatabase;

/**
 * The application name that will be used when a crash report is submitted.
 *
 * By default, this value is pulled from the App's Info.plist (CFBundleDisplayName or CFBundleName).
 *
 * Set this property to override the default application name. The value can be changed at any time
 * and will be captured at crash time.
 */
@property (nonatomic, copy, nullable) NSString *applicationName;

/**
 * The application version that will be used when a crash report is submitted.
 *
 * By default, this value is pulled from the App's Info.plist (CFBundleShortVersionString).
 *
 * Set this property to override the default application version. The value can be changed at any time
 * and will be captured at crash time.
 */
@property (nonatomic, copy, nullable) NSString *applicationVersion;

/**
 * The user name that will be used when a crash report is submitted.
 *
 * The value can be set programmatically at any time and will be stored in NSUserDefaults.
 * To delete the value from NSUserDefaults, set the value to `nil`.
 *
 * This property is optional.
 *
 * @warning When returning a non nil value, crash reports are not anonymous any more
 * and the crash alerts will not show the word "anonymous"!
 *
 * @warning If setting this property programmatically, it needs to be set before calling `start`
 * if the userName should be included in a possible crash from the last app session.
 *
 * @see userEmail
 */
@property (nonatomic, copy, nullable) NSString *userName;

/**
 * The user email address that will be used when a crash report is submitted.
 *
 * The value can be set programmatically at any time and will be stored in NSUserDefaults.
 * To delete the value from NSUserDefaults, set the value to `nil`.
 *
 * This property is optional.
 *
 * @warning When returning a non nil value, crash reports are not anonymous any more
 * and the crash alerts will not show the word "anonymous"!
 *
 * @warning If setting this property programmatically, it needs to be set before calling `start`
 * if the userEmail should be included in a possible crash from the last app session.
 *
 * @see userName
 */
@property (nonatomic, copy, nullable) NSString *userEmail;

/**
 * An application-defined key that will be included with crash reports.
 *
 * Use this for any identifier meaningful to your application, such as:
 * - License keys
 * - Build identifiers
 * - User segments
 * - Environment identifiers (dev/staging/prod)
 *
 * In the BugSplat dashboard, you can configure custom localized support responses
 * for crash groups based on the appKey value.
 *
 * The value is captured at crash time and included with the crash report.
 */
@property (nonatomic, copy, nullable) NSString *appKey;

/**
 * Additional notes to include with crash reports.
 *
 * Use this for any additional context about the crash, such as:
 * - Build configuration details
 * - Feature flags enabled
 * - Recent user actions
 * - Debug information
 *
 * The value is captured at crash time and included with the crash report.
 */
@property (nonatomic, copy, nullable) NSString *notes;

/*!
 *  Submit crash reports without asking the user
 *
 *  _YES_: The crash report will be submitted without asking the user
 *  _NO_: The user will be asked if the crash report can be submitted
 *
 *  On iOS, when set to NO, the user is presented with an alert with options:
 *  - "Send" - sends this crash report
 *  - "Don't Send" - discards all pending crash reports
 *  - "Always Send" - sends this crash report and enables auto-submit for future crashes
 *
 *  Default: iOS: _YES_, macOS: _NO_
 */
@property (nonatomic, assign) BOOL autoSubmitCrashReport;

/**
 * Enable detection and reporting of fatal main-thread hangs.
 *
 * When set to YES before `-start` is invoked, BugSplat monitors the main runloop for
 * prolonged unresponsive periods while the app is active in the foreground. If a hang
 * is detected and the app is subsequently terminated without the main thread recovering
 * (launch/resume watchdog kills, user force-quit), a hang report is uploaded on the next
 * launch using the same pipeline as crash reports.
 *
 * If the main thread resumes after a hang is detected, the persisted report is discarded -
 * non-fatal hangs are not reported in this version.
 *
 * Hang reports carry the exception name `App Hang (Fatal)` and include attributes prefixed
 * with `bugsplat-hang-` (duration, detection time, app state, launch id) that can be used
 * to correlate with crashes from the same launch.
 *
 * Detection is suppressed when a debugger is attached or the app is not active. As a
 * consequence, hangs that begin while the app is in the background (including those
 * terminated by background-task expiration) are not reported.
 *
 * This property is a no-op inside app extensions.
 *
 * When this property is YES, `-start` must be invoked on the main thread - the
 * main thread's Mach port is captured there so the hang report identifies the
 * correct thread. Debug builds assert; Release builds will silently capture the
 * wrong thread.
 *
 * Default: NO
 */
@property (nonatomic, assign) BOOL enableHangDetection;

/**
 * Threshold in seconds for declaring the main thread hung when `enableHangDetection` is YES.
 *
 * Must be set before `-start` is invoked. Values below 0.1 are clamped to 0.1 by the
 * underlying tracker. Typical production values are 1.0-5.0 seconds; choose a value above
 * any work the app may legitimately do on the main thread (image decoding, JSON parsing,
 * etc.) to avoid false positives.
 *
 * Default: 2.0
 */
@property (nonatomic, assign) NSTimeInterval hangDetectionThreshold;

/**
 * Submit fatal hang reports without asking the user, the hang counterpart to
 * `autoSubmitCrashReport`.
 *
 * Fatal hang reports are persisted while the main thread is wedged and uploaded on the next
 * launch. They are auto-submitted by default: the user never had the chance to consent,
 * because the app was frozen and then terminated, so the report is marked as already
 * submitted when it is persisted and the next-launch scanner uploads it without a dialog.
 *
 * Set this to NO to ask instead. The report then takes the same submission path a crash report
 * does, so the user can describe what the app was doing when it froze - usually the only thing
 * that makes a hang actionable.
 *
 * Note that when this is NO, whether a dialog actually appears still follows
 * `autoSubmitCrashReport`, exactly as it does for a crash: macOS defaults that to NO and shows
 * the dialog, while iOS defaults it to YES and submits silently either way.
 *
 * Has no effect unless `enableHangDetection` is YES.
 *
 * Default: YES
 */
@property (nonatomic, assign) BOOL autoSubmitFatalHangReport;

/**
 * Add an attribute and value to a dictionary of attributes that will potentially be included in a crash report.
 * If the attribute name is nil or empty, or the attribute+value pair cannot be set, the method will return NO,
 * otherwise it will return YES.
 *
 * Attributes and values represent app supplied keys and values to associate with a crash report, should the app crash during this session.
 * Attributes are sent with the crash report as form fields on the crash upload request, on both macOS and iOS.
 * They are NOT sent as a BugSplatAttachment, so attributes and attachments are independent - supplying
 * attachments via `BugSplatDelegate` never suppresses attributes, and attributes never consume an attachment slot.
 *
 * NOTES:
 *
 * This method may be called multiple times, once per attribute+value pair.
 * This method may be called at any time during the app session prior to a crash.
 * Attributes are stored in an NSDictionary<NSString *, NSString *>, so attribute names must be unique.
 * If the attribute does not exist, it will be added to attributes dictionary.
 * If attribute already exists, the value will be replaced in the dictionary.
 * If attribute already exists, and the value is nil, the attribute will be removed from the dictionary.
 *
 * Attributes are recorded with the crash report when a crash occurs, and are uploaded with that report
 * during the next launch of the app.
 *
 * Attributes and their values are only valid for the lifetime of the app session and only used in a crash report if the crash occurs during that app session.
 * If the app terminates normally, any attributes recorded during the prior `normal` app session are discarded.
 *
 */
- (BOOL)setValue:(nullable NSString *)value forAttribute:(NSString *)attribute NS_SWIFT_NAME(set(_:for:));

/**
 * Submits user feedback (non-crash) to BugSplat.
 *
 * This sends a feedback report using crash type ID 36 (User.Feedback).
 * The feedback is packaged as a JSON file and uploaded via the standard
 * presigned URL flow.
 *
 * @param title The feedback title (required).
 * @param description Optional description providing additional detail.
 * @param userName Optional user name. Falls back to the `userName` property if nil.
 * @param userEmail Optional user email. Falls back to the `userEmail` property if nil.
 * @param appKey Optional application key. Falls back to the `appKey` property if nil.
 * @param attachments Optional array of file attachments to include with the feedback.
 * @param completion Optional completion handler called when the upload finishes.
 *                   The error parameter is nil on success.
 *
 * @note To send custom attributes with the feedback, or to receive the report id
 *       of the submitted feedback, use
 *       `-postFeedback:description:userName:userEmail:appKey:attributes:attachments:completion:`.
 */
- (void)postFeedback:(NSString *)title
         description:(nullable NSString *)description
            userName:(nullable NSString *)userName
           userEmail:(nullable NSString *)userEmail
              appKey:(nullable NSString *)appKey
         attachments:(nullable NSArray<BugSplatAttachment *> *)attachments
          completion:(nullable void (^)(NSError * _Nullable error))completion
    NS_SWIFT_NAME(postFeedback(title:description:userName:userEmail:appKey:attachments:completion:));

/**
 * Submits user feedback (non-crash) to BugSplat, with custom attributes, and reports
 * the resulting report id.
 *
 * Behaves identically to
 * `-postFeedback:description:userName:userEmail:appKey:attachments:completion:` with
 * two additions:
 *  - the supplied `attributes` are sent with the feedback and are searchable in the
 *    BugSplat dashboard, and
 *  - on success the completion handler receives a `BugSplatFeedbackResult` containing
 *    the report id (`crashId`) and `infoUrl` of the submitted feedback.
 *
 * @param title The feedback title (required).
 * @param description Optional description providing additional detail.
 * @param userName Optional user name. Falls back to the `userName` property if nil.
 * @param userEmail Optional user email. Falls back to the `userEmail` property if nil.
 * @param appKey Optional application key. Falls back to the `appKey` property if nil.
 * @param attributes Optional custom string key/value attributes to associate with the
 *                   feedback, e.g. @{@"category": @"Bug"}.
 * @param attachments Optional array of file attachments to include with the feedback.
 * @param completion Optional completion handler called when the upload finishes.
 *                   On success, `result` is non-nil and `error` is nil.
 *                   On failure, `result` is nil and `error` is set.
 */
- (void)postFeedback:(NSString *)title
         description:(nullable NSString *)description
            userName:(nullable NSString *)userName
           userEmail:(nullable NSString *)userEmail
              appKey:(nullable NSString *)appKey
          attributes:(nullable NSDictionary<NSString *, NSString *> *)attributes
         attachments:(nullable NSArray<BugSplatAttachment *> *)attachments
          completion:(nullable void (^)(BugSplatFeedbackResult * _Nullable result, NSError * _Nullable error))completion
    NS_SWIFT_NAME(postFeedback(title:description:userName:userEmail:appKey:attributes:attachments:completion:));

// macOS specific API
#if TARGET_OS_OSX
/*!
 *  Provide custom banner image for crash reporter.
 *  Can set directly in code or provide an image named bugsplat-logo in main bundle. Can be in asset catalog.
 */
@property (nonatomic, strong, nullable) NSImage *bannerImage;

/**
 *  Defines if the crash report UI should ask for name and email
 *
 *  Default: _YES_
 */
@property (nonatomic, assign) BOOL askUserDetails;

/**
 * If the user enters their name or email on a Bug Crash Alert Form, persist their data to NSUserDefaults.
 * After this occurs, userName and userEmail properties will contain the values the user entered.
 * When the Bug Crash Alert Form is presented again, it will be pre-populated with user name and email.
 * To erase their user name or email, set the property value to nil programmatically.
 *
 * This property defaults to NO.
 * This property is optional.
 *
 * @warning If setting this property to YES, it needs to be set before calling `start`.
 *
 * @see userName
 * @see userEmail
 */
@property (nonatomic, assign) BOOL persistUserDetails;

/**
 *  Defines if crash reports should be considered "expired" after a certain amount of time (in seconds).
 *  If expired crash dialogue is not displayed but reports are still uploaded.
 *
 *  Default: -1 // No expiration
 */
@property (nonatomic, assign) NSTimeInterval expirationTimeInterval;

/**
 * Option to present crash reporter dialogue modally
 *
 * *Default*:  NO
 */
@property (nonatomic, assign) BOOL presentModally;

#endif

@end

NS_ASSUME_NONNULL_END
