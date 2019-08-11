#import <XCTest/XCTest.h>
#import "ScalarMockAboutWindowController.h"
#import "ScalarMockProductInfoFetcher.h"
#import "ScalarStatusBarItem.h"

NSString * const ExpectedAboutMenuTitle = @"About Scalar";

@interface ScalarStatusBarItemTests : XCTestCase

@property (strong) ScalarStatusBarItem *statusbarItem;
@property (strong) ScalarMockAboutWindowController *aboutWindowController;

@end

@implementation ScalarStatusBarItemTests

- (void)setUp
{
    [super setUp];
    
    ScalarMockProductInfoFetcher *mockProductInfoFetcher = [[ScalarMockProductInfoFetcher alloc]
                                                         initWithGitVersion:@""
                                                         scalarVersion:@""];
    
    self.aboutWindowController = [[ScalarMockAboutWindowController alloc]
                                  initWithProductInfoFetcher:mockProductInfoFetcher];
    self.statusbarItem = [[ScalarStatusBarItem alloc]
                          initWithAboutWindowController:self.aboutWindowController];
    
    [self.statusbarItem load];
}

- (void)tearDown
{
    [super tearDown];
}

- (void)testStatusItemContainsAboutMenu
{
    NSMenu *statusMenu = [self.statusbarItem getStatusMenu];
    XCTAssertNotNil(statusMenu, @"Status bar does not contain Scalar menu");
    
    NSMenuItem *menuItem = [statusMenu itemWithTitle:ExpectedAboutMenuTitle];
    XCTAssertNotNil(menuItem, @"Missing \"%@\" item in Scalar menu", ExpectedAboutMenuTitle);
}

- (void)testAboutMenuClickDisplaysAboutBox
{
    [self.statusbarItem handleMenuClick:nil];
    
    XCTAssertTrue(self.aboutWindowController.aboutBoxDisplayed,
                  @"Clicking on \"%@\" menu does not show About box",
                  ExpectedAboutMenuTitle);
}

@end
