//
//  BugSplatReportResult.h
//
//  Copyright © BugSplat, LLC. All rights reserved.
//

#import <Foundation/Foundation.h>

NS_ASSUME_NONNULL_BEGIN

/**
 * Identifies a report that was accepted by the BugSplat server.
 *
 * Returned via the completion handler of the APIs that upload a report while the app keeps
 * running - `-postException:...`, `-postError:...`, and (as the `BugSplatFeedbackResult`
 * subclass) `-postFeedback:...`. Both properties are populated from the `commitS3CrashUpload`
 * server response and may be nil if the server response did not include the corresponding value.
 */
@interface BugSplatReportResult : NSObject

/**
 * The BugSplat report id assigned to the submitted report.
 *
 * Use this to deep-link to the report, e.g.
 * `https://app.bugsplat.com/v2/crash?database={database}&id={crashId}`.
 */
@property (nonatomic, readonly, copy, nullable) NSNumber *crashId;

/**
 * URL to the report details page returned by the server.
 */
@property (nonatomic, readonly, copy, nullable) NSString *infoUrl;

/**
 * Designated initializer.
 *
 * @param crashId The BugSplat report id, or nil if unavailable.
 * @param infoUrl The report info URL, or nil if unavailable.
 */
- (instancetype)initWithCrashId:(nullable NSNumber *)crashId
                        infoUrl:(nullable NSString *)infoUrl NS_DESIGNATED_INITIALIZER;

- (instancetype)init NS_UNAVAILABLE;
+ (instancetype)new NS_UNAVAILABLE;

@end

NS_ASSUME_NONNULL_END
