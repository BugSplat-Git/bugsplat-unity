//
//  BugSplatDelegate.h
//
//  Copyright © BugSplat, LLC. All rights reserved.
//

#import <TargetConditionals.h>
#import <Foundation/Foundation.h>

NS_ASSUME_NONNULL_BEGIN

@class BugSplat;
@class BugSplatAttachment;

@protocol BugSplatDelegate <NSObject>

@optional

// MARK: - BugSplatDelegate (MacOS, iOS)

/** Return any log string based data the crash report being processed should contain
 *
 * @param bugSplat The `BugSplat` instance invoking this delegate
 */
- (NSString *)applicationLogForBugSplat:(BugSplat *)bugSplat;

/** Return any log string based data the crash report being processed should contain
 *
 * When implemented, this method is preferred over `applicationLogForBugSplat:`.
 *
 * @param bugSplat The `BugSplat` instance invoking this delegate
 * @param sessionID The ID of the session that crashed (the value of `BugSplat.sessionID`
 *        during the session the report was recorded in), or nil if the report predates
 *        session tracking. Use it to look up log data you recorded for that session.
 */
- (nullable NSString *)applicationLogForBugSplat:(BugSplat *)bugSplat sessionID:(nullable NSUUID *)sessionID;

/** Invoked right before sending crash reports will start

 @param bugSplat The `BugSplat` instance invoking this delegate
 */
- (void)bugSplatWillSendCrashReport:(BugSplat *)bugSplat;

/** Invoked right before sending a crash report will start

 When implemented, this method is preferred over `bugSplatWillSendCrashReport:`.

 @param bugSplat The `BugSplat` instance invoking this delegate
 @param sessionID The ID of the session the crash report being sent was recorded in,
        or nil if the report predates session tracking
 */
- (void)bugSplatWillSendCrashReport:(BugSplat *)bugSplat sessionID:(nullable NSUUID *)sessionID;

/** Invoked after sending crash reports failed

 @param bugSplat The `BugSplat` instance invoking this delegate
 @param error The error returned from the NSURLSession call
 */
- (void)bugSplat:(BugSplat *)bugSplat didFailWithError:(NSError *)error;

/** Invoked after sending a crash report failed

 When implemented, this method is preferred over `bugSplat:didFailWithError:`.

 @param bugSplat The `BugSplat` instance invoking this delegate
 @param error The error returned from the NSURLSession call
 @param sessionID The ID of the session the crash report was recorded in,
        or nil if the report predates session tracking
 */
- (void)bugSplat:(BugSplat *)bugSplat didFailWithError:(NSError *)error sessionID:(nullable NSUUID *)sessionID;

/** Invoked after sending crash reports succeeded

 @param bugSplat The `BugSplat` instance invoking this delegate
 */
- (void)bugSplatDidFinishSendingCrashReport:(BugSplat *)bugSplat;

/** Invoked after sending a crash report succeeded

 When implemented, this method is preferred over `bugSplatDidFinishSendingCrashReport:`.

 Use the sessionID to clean up any session-scoped data (such as a per-session log
 file returned from `attachmentsForBugSplat:sessionID:`) that is no longer needed
 once the crash report has been delivered. This is invoked once per report, so the
 correct session can be identified even when multiple pending reports are uploaded.

 @param bugSplat The `BugSplat` instance invoking this delegate
 @param sessionID The ID of the session the crash report was recorded in,
        or nil if the report predates session tracking
 */
- (void)bugSplatDidFinishSendingCrashReport:(BugSplat *)bugSplat sessionID:(nullable NSUUID *)sessionID;

/** Invoked before the user is asked to send a crash report, so you can do additional actions.
 E.g. to make sure not to ask the user for an app rating :)

 @param bugSplat The `BugSplat` instance invoking this delegate
 */
-(void)bugSplatWillShowSubmitCrashReportAlert:(BugSplat *)bugSplat;

/** Invoked after the user did choose _NOT_ to send a crash in the alert

 @param bugSplat The `BugSplat` instance invoking this delegate
 */
-(void)bugSplatWillCancelSendingCrashReport:(BugSplat *)bugSplat;

/** Return a collection of BugSplatAttachment objects that the crash report being processed should contain

 All returned attachments are included with the crash report, on both macOS and iOS.

 Example implementation:

 NSData *data = [NSData dataWithContentsOfFile:@"mydatafile"];

 BugSplatAttachment *attachment = [[BugSplatAttachment alloc] initWithFilename:@"myfile.data"
                                                                attachmentData:data
                                                                   contentType:@"application/octet-stream"];
 return @[attachment];

 @param bugSplat The `BugSplat` instance invoking this delegate
*/
- (NSArray<BugSplatAttachment *> *)attachmentsForBugSplat:(BugSplat *)bugSplat API_AVAILABLE(macosx(10.13), ios(13.0));

/** Return a collection of BugSplatAttachment objects that the crash report being processed should contain

 When implemented, this method is preferred over `attachmentsForBugSplat:` and the
 single-attachment variants.

 All returned attachments are included with the crash report, on both macOS and iOS.

 The sessionID identifies the session that crashed, so attachments for that
 specific session can be returned. The recommended pattern is to record a
 mapping from `BugSplat.sessionID` to session-scoped file paths (e.g. this
 session's log file) right after calling `start`, then use the sessionID passed
 here to look up and return the matching files:

 NSString *logPath = [self logPathForSessionID:sessionID];  // your own mapping
 NSData *data = [NSData dataWithContentsOfFile:logPath];

 BugSplatAttachment *attachment = [[BugSplatAttachment alloc] initWithFilename:@"session.log"
                                                                attachmentData:data
                                                                   contentType:@"text/plain"];
 return @[attachment];

 @param bugSplat The `BugSplat` instance invoking this delegate
 @param sessionID The ID of the session that crashed, or nil if the crash report
        predates session tracking (in which case you may fall back to a heuristic
        or return an empty array)
*/
- (NSArray<BugSplatAttachment *> *)attachmentsForBugSplat:(BugSplat *)bugSplat sessionID:(nullable NSUUID *)sessionID API_AVAILABLE(macosx(10.13), ios(13.0));

/** Return a BugSplatAttachment object providing an NSData object the crash report being processed should contain

 Prefer `attachmentsForBugSplat:sessionID:` when the report should carry more than
 one attachment.

 Example implementation:

 NSData *data = [NSData dataWithContentsOfFile:@"mydatafile"];

 BugSplatAttachment *attachment = [[BugSplatAttachment alloc] initWithFilename:@"myfile.data"
                                                                attachmentData:data
                                                                   contentType:@"application/octet-stream"];
 return attachment;

 @param bugSplat The `BugSplat` instance invoking this delegate
*/
- (nullable BugSplatAttachment *)attachmentForBugSplat:(BugSplat *)bugSplat API_AVAILABLE(ios(13.0));

/** Return a BugSplatAttachment object providing an NSData object the crash report being processed should contain

 When implemented, this method is preferred over `attachmentForBugSplat:`.
 Prefer `attachmentsForBugSplat:sessionID:` when the report should carry more than
 one attachment.

 The sessionID identifies the session that crashed, so the attachment for that
 specific session can be returned. The recommended pattern is to record a
 mapping from `BugSplat.sessionID` to session-scoped file paths (e.g. this
 session's log file) right after calling `start`, then use the sessionID passed
 here to look up and return the matching file:

 NSString *logPath = [self logPathForSessionID:sessionID];  // your own mapping
 NSData *data = [NSData dataWithContentsOfFile:logPath];

 BugSplatAttachment *attachment = [[BugSplatAttachment alloc] initWithFilename:@"session.log"
                                                                attachmentData:data
                                                                   contentType:@"text/plain"];
 return attachment;

 @param bugSplat The `BugSplat` instance invoking this delegate
 @param sessionID The ID of the session that crashed, or nil if the crash report
        predates session tracking (in which case you may fall back to a heuristic
        or return nil)
*/
- (nullable BugSplatAttachment *)attachmentForBugSplat:(BugSplat *)bugSplat sessionID:(nullable NSUUID *)sessionID API_AVAILABLE(ios(13.0));

// MARK: - BugSplatDelegate (iOS)
#if !TARGET_OS_OSX

/** Invoked after the user chose "Always Send" in the crash report alert.
 
 When the user selects "Always Send", the crash report is sent and future crash reports
 will be submitted automatically without prompting (unless the app clears NSUserDefaults).
 
 Use this delegate method to update your app's UI or settings to reflect the user's choice.

 @param bugSplat The `BugSplat` instance invoking this delegate
 */
-(void)bugSplatWillSendCrashReportsAlways:(BugSplat *)bugSplat API_AVAILABLE(ios(13.0));

#endif

@end

NS_ASSUME_NONNULL_END
