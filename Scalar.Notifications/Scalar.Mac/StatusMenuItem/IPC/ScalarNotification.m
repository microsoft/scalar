#import "ScalarNotification.h"
#import "ScalarNotificationErrors.h"

NSString * const IdentifierKey = @"Id";
NSString * const EnlistmentKey = @"Enlistment";
NSString * const EnlistmentCountKey = @"EnlistmentCount";
NSString * const TitleKey = @"Title";
NSString * const MessageKey = @"Message";

NSString * const AutomountTitle = @"Scalar AutoMount";
NSString * const AutomountStartMessageFormat = @"Attempting to mount %lu Scalar repos(s)";
NSString * const AutomountSuccessMessageFormat = @"The following Scalar repo is now mounted: \n%@";
NSString * const AutomountFailureMessageFormat = @"The following Scalar repo failed to mount: \n%@";

@interface ScalarNotification()

@property (readwrite) NSString *title;
@property (readwrite) NSString *message;

NS_ASSUME_NONNULL_BEGIN

- (instancetype _Nullable)initAsMountSuccessWithMessage:(NSDictionary *)messageDict
                                                  error:(NSError *__autoreleasing *)error;

- (instancetype _Nullable)initAsMountFailureWithMessage:(NSDictionary *)messageDict
                                                  error:(NSError *__autoreleasing *)error;

- (instancetype _Nullable)initAsMountWithMessage:(NSDictionary *)messageDict
                                           title:(NSString *)title
                                   messageFormat:(NSString *)messageFormat
                                           error:(NSError *__autoreleasing *)error;

NS_ASSUME_NONNULL_END

@end

@implementation ScalarNotification

+ (BOOL)tryValidateMessage:(NSDictionary *)jsonMessage
         buildNotification:(ScalarNotification **)notification
                     error:(NSError *__autoreleasing *)error
{
    NSParameterAssert(notification);
    NSParameterAssert(jsonMessage);
    
    id identifier = jsonMessage[IdentifierKey];
    if (![identifier isKindOfClass:[NSNumber class]])
    {
        if (error != nil)
        {
            *error = [NSError errorWithDomain:ScalarNotificationErrorDomain
                                         code:ScalarInvalidMessageIdFormatError
                                     userInfo:@{ NSLocalizedDescriptionKey : @"Unexpected message id/format)" }];
        }
        
        return NO;
    }
    
    Identifier idValue = [identifier integerValue];
    NSError *initError = nil;
    switch (idValue)
    {
        case AutomountStart:
        {
            *notification = [[ScalarNotification alloc]
                             initAsAutomountStartWithMessage:jsonMessage
                             error:&initError];
            break;
        }
        
        case MountSuccess:
        {
            *notification = [[ScalarNotification alloc]
                             initAsMountSuccessWithMessage:jsonMessage
                             error:&initError];
            break;
        }
            
        case MountFailure:
        {
            *notification = [[ScalarNotification alloc]
                             initAsMountFailureWithMessage:jsonMessage
                             error:&initError];
            break;
        }
            
        default:
        {
            *notification = nil;
            initError = [NSError errorWithDomain:ScalarNotificationErrorDomain
                                            code:ScalarUnsupportedMessageError
                                        userInfo:@{ NSLocalizedDescriptionKey : @"Unrecognised message id" }];
            break;
        }
    }
    
    if (error != nil)
    {
        *error = initError;
    }
    
    return *notification != nil;
}

#pragma mark Private initializers

- (instancetype)initAsAutomountStartWithMessage:(NSDictionary *)messageDict
                                          error:(NSError *__autoreleasing *)error
{
    if (self = [super init])
    {
        id repoCount = messageDict[EnlistmentCountKey];
        if (repoCount && [repoCount isKindOfClass:[NSNumber class]])
        {
            _title = [AutomountTitle copy];
            _message = [[NSString stringWithFormat:AutomountStartMessageFormat, [repoCount unsignedIntegerValue]] copy];
            return self;
        }
        
        if (error != nil)
        {
            *error = [NSError errorWithDomain:ScalarNotificationErrorDomain
                                         code:ScalarMissingRepoCountError
                                     userInfo:@{ NSLocalizedDescriptionKey : @"Missing repos count in AutomountStart message" }];
        }
        
        self = nil;
    }
    
    return self;
}

- (instancetype)initAsMountSuccessWithMessage:(NSDictionary *)messageDict
                                        error:(NSError *__autoreleasing *)error
{
    return self = [self initAsMountWithMessage:messageDict
                                         title:(NSString *)AutomountTitle
                                 messageFormat:(NSString *)AutomountSuccessMessageFormat
                                         error:error];
}

- (instancetype)initAsMountFailureWithMessage:(NSDictionary *)messageDict
                                        error:(NSError *__autoreleasing *)error
{
    return self = [self initAsMountWithMessage:messageDict
                                         title:(NSString *)AutomountTitle
                                 messageFormat:(NSString *)AutomountFailureMessageFormat
                                         error:error];
}

- (instancetype)initAsMountWithMessage:(NSDictionary *)messageDict
                                 title:(NSString *)title
                         messageFormat:(NSString *)messageFormat
                                 error:(NSError *__autoreleasing *)error
{
    NSParameterAssert(title);
    NSParameterAssert(messageFormat);
    
    if (self = [super init])
    {
        id enlistment = messageDict[EnlistmentKey];
        if (enlistment && [enlistment isKindOfClass:[NSString class]])
        {
            _title = [title copy];
            _message = [[NSString stringWithFormat:
                         (NSString *)messageFormat,
                         enlistment] copy];
            return self;
        }
        
        if (error != nil)
        {
            *error = [NSError errorWithDomain:ScalarNotificationErrorDomain
                                         code:ScalarMissingEntitlementInfoError
                                     userInfo:@{ NSLocalizedDescriptionKey : @"ERROR: missing enlistment info." }];
        }
        
        self = nil;
    }
    
    return self;
}

@end
