#import <Cocoa/Cocoa.h>

@interface ScalarStatusBarItem : NSObject

- (instancetype _Nullable)initWithAboutWindowController:(ScalarAboutWindowController *_Nonnull)aboutWindowController;
- (void)load;
- (NSMenu *_Nullable)getStatusMenu;
- (IBAction)handleMenuClick:(id)sender;

@end
