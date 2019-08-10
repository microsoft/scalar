#import <objc/runtime.h>
#import "ScalarProductInfoFetcher.h"

NSString * const ScalarPath = @"/usr/local/bin/scalar";
NSString * const GitPath = @"/usr/local/bin/git";

@interface ScalarProductInfoFetcher()

@property (strong, nonnull) ScalarProcessRunner *processRunner;

@end

@implementation ScalarProductInfoFetcher

- (instancetype)initWithProcessRunner:(ScalarProcessRunner *)processRunner
{
    if (processRunner == nil)
    {
        self = nil;
    }
    else if (self = [super init])
    {
        _processRunner = processRunner;
    }
    
    return self;
}

- (BOOL)tryGetScalarVersion:(NSString *__autoreleasing *)version
                         error:(NSError *__autoreleasing *)error
{
    NSParameterAssert(version);
    
    if (![self.processRunner tryRunExecutable:[NSURL fileURLWithPath:ScalarPath]
                                         args:@[ @"version" ]
                                       output:version
                                        error:error])
    {
        return NO;
    }
    
    return YES;
}

- (BOOL)tryGetGitVersion:(NSString *__autoreleasing *)version
                   error:(NSError *__autoreleasing *)error
{
    NSParameterAssert(version);
    
    if (![self.processRunner tryRunExecutable:[NSURL fileURLWithPath:GitPath]
                                         args:@[ @"version" ]
                                       output:version
                                        error:error])
    {
        return NO;
    }
    
    return YES;
}

@end
