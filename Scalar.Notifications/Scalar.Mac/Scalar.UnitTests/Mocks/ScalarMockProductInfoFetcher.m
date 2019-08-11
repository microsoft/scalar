#import <Foundation/Foundation.h>
#import "ScalarMockProductInfoFetcher.h"

@interface ScalarMockProductInfoFetcher()

@property (copy) NSString *gitVersion;
@property (copy) NSString *scalarVersion;

@end

@implementation ScalarMockProductInfoFetcher

- (instancetype) initWithGitVersion:(NSString *) gitVersion
                   scalarVersion:(NSString *) scalarVersion
{
    if (self = [super init])
    {
        _gitVersion = [gitVersion copy];
        _scalarVersion = [scalarVersion copy];
    }
    
    return self;
}

- (BOOL) tryGetScalarVersion:(NSString *__autoreleasing *) version
                          error:(NSError *__autoreleasing *) error
{
    *version = self.scalarVersion;
    return YES;
}

- (BOOL) tryGetGitVersion:(NSString *__autoreleasing *) version
                    error:(NSError *__autoreleasing *) error
{
    *version = self.gitVersion;
    return YES;
}

@end
