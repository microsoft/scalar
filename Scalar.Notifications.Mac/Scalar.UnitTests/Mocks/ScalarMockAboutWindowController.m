#import <Foundation/Foundation.h>
#import "ScalarMockAboutWindowController.h"

@interface ScalarMockAboutWindowController()

@property (readwrite) BOOL aboutBoxDisplayed;

@end

@implementation ScalarMockAboutWindowController

- (instancetype) initWithProductInfo:(ScalarProductInfoFetcher *) productInfo
{
    self = [super initWithProductInfoFetcher:productInfo];
    return self;
}

- (IBAction)showWindow:(nullable id)sender
{
    self.aboutBoxDisplayed = YES;
}

@end
