#import "ScalarAboutWindowController.h"
#import "ScalarAppDelegate.h"
#import "ScalarMessageListener.h"
#import "ScalarNotificationDisplay.h"
#import "ScalarNotification.h"
#import "ScalarProductInfoFetcher.h"
#import "ScalarStatusBarItem.h"
#import "ScalarNotificationDisplay.h"

@interface ScalarAppDelegate ()

@property (weak) IBOutlet NSWindow *Window;
@property (strong) ScalarStatusBarItem *StatusDisplay;
@property (strong) ScalarMessageListener *messageListener;

- (void)displayNotification:(NSDictionary *_Nonnull)messageInfo;

@end

@implementation ScalarAppDelegate

- (void)applicationDidFinishLaunching:(NSNotification *)aNotification
{
    self.messageListener = [[ScalarMessageListener alloc]
        initWithSocket:NSTemporaryDirectory()
        callback:^(NSDictionary *messageInfo)
        {
            [self displayNotification:messageInfo];
        }];
    
    [self.messageListener startListening];
    
    ScalarProductInfoFetcher *productInfoFetcher =
    [[ScalarProductInfoFetcher alloc]
     initWithProcessRunner:[[ScalarProcessRunner alloc] initWithProcessFactory:^NSTask *
                            {
                                return [[NSTask alloc] init];
                            }]];
    
    self.StatusDisplay = [[ScalarStatusBarItem alloc] initWithAboutWindowController:
                          [[ScalarAboutWindowController alloc]
                           initWithProductInfoFetcher:productInfoFetcher]];
    
    [self.StatusDisplay load];
}

- (void)applicationWillTerminate:(NSNotification *)aNotification
{
    [self.messageListener stopListening];
}

- (void)displayNotification:(NSDictionary *_Nonnull)messageInfo
{
    NSParameterAssert(messageInfo);
    
    ScalarNotification *notification;
    NSError *error;
    if (![ScalarNotification tryValidateMessage:messageInfo
                                 buildNotification:&notification
                                             error:&error])
    {
        NSLog(@"ERROR: Could not display notification. %@", [error description]);
        return;
    }
    
    ScalarNotificationDisplay *notificationDisplay =
    [[ScalarNotificationDisplay alloc] initWithTitle:notification.title
                                          message:notification.message];
    
    [notificationDisplay display];
}
@end
