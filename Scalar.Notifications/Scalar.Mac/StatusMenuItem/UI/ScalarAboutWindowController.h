#import <Cocoa/Cocoa.h>
#import "ScalarProductInfoFetcher.h"

@interface ScalarAboutWindowController : NSWindowController

@property (readonly, nullable) NSString *scalarVersion;
@property (readonly, nullable) NSString *gitVersion;

- (instancetype _Nullable)initWithProductInfoFetcher:(ScalarProductInfoFetcher *_Nonnull)productInfoFetcher;

@end
