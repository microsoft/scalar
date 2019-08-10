#import <Foundation/Foundation.h>
#import "ScalarProductInfoFetcher.h"

NS_ASSUME_NONNULL_BEGIN

@interface ScalarMockProductInfoFetcher : ScalarProductInfoFetcher

- (instancetype _Nullable) initWithGitVersion:(NSString *) gitVersion
                             scalarVersion:(NSString *) scalarVersion;

@end

NS_ASSUME_NONNULL_END
