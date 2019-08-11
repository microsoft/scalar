#import "ScalarAboutWindowController.h"

@interface ScalarAboutWindowController ()

@property (strong) ScalarProductInfoFetcher *productInfoFetcher;

@end

@implementation ScalarAboutWindowController

- (instancetype)initWithProductInfoFetcher:(ScalarProductInfoFetcher *)productInfoFetcher
{
    if (productInfoFetcher == nil)
    {
        self = nil;
    }
    else if (self = [super initWithWindowNibName:@"ScalarAboutWindowController"])
    {
        _productInfoFetcher = productInfoFetcher;
    }
    
    return self;
}

- (NSString *)scalarVersion
{
    NSString *version;
    NSError *error;
    if ([self.productInfoFetcher tryGetScalarVersion:&version error:&error])
    {
        return version;
    }
    else
    {
        NSLog(@"Error getting Scalar version: %@", [error description]);
        return @"Not available";
    }
}

- (NSString *)gitVersion
{
    NSString *version;
    NSError *error;
    if ([self.productInfoFetcher tryGetGitVersion:&version error:&error])
    {
        return version;
    }
    else
    {
        NSLog(@"Error getting Git version: %@", [error description]);
        return @"Not available";
    }
}

@end
