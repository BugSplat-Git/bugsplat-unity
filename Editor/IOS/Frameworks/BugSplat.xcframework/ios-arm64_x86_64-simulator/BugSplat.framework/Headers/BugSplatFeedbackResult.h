//
//  BugSplatFeedbackResult.h
//
//  Copyright © BugSplat, LLC. All rights reserved.
//

#import <Foundation/Foundation.h>
#import <BugSplat/BugSplatReportResult.h>

NS_ASSUME_NONNULL_BEGIN

/**
 * The result of a successful user-feedback submission.
 *
 * Returned via the completion handler of
 * `-postFeedback:description:userName:userEmail:appKey:attributes:attachments:completion:`.
 * `crashId` and `infoUrl` are inherited from `BugSplatReportResult`.
 *
 * Note: feedback reports group by their (unique) title, so `infoUrl` may resolve to a
 * generic page. To link to a specific report, prefer building a URL from `crashId`.
 */
@interface BugSplatFeedbackResult : BugSplatReportResult

@end

NS_ASSUME_NONNULL_END
