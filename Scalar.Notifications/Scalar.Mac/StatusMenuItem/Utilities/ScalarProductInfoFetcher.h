#import <Foundation/Foundation.h>
#import "ScalarProcessRunner.h"

NS_ASSUME_NONNULL_BEGIN

@interface ScalarProductInfoFetcher : NSObject

- (instancetype _Nullable)initWithProcessRunner:(ScalarProcessRunner *)processRunner;
- (BOOL)tryGetGitVersion:(NSString *_Nullable __autoreleasing *_Nonnull)version
                   error:(NSError **)error;
- (BOOL)tryGetScalarVersion:(NSString *_Nullable __autoreleasing *_Nonnull)version
                         error:(NSError **)error;

@end

NS_ASSUME_NONNULL_END
