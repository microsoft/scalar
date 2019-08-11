#import <Foundation/Foundation.h>

NS_ASSUME_NONNULL_BEGIN

extern NSErrorDomain const ScalarNotificationErrorDomain;
typedef NS_ERROR_ENUM(ScalarNotificationErrorDomain, ScalarNotificationErrorCode)
{
    ScalarInitError = 200,
    ScalarAllocError,
    ScalarInvalidMessageIdFormatError,
    ScalarUnsupportedMessageError,
    ScalarMissingEntitlementInfoError,
    ScalarMissingRepoCountError,
    ScalarMessageParseError,
    ScalarMessageReadError,
};

NS_ASSUME_NONNULL_END
