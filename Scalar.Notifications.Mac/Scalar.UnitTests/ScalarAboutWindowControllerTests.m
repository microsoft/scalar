#import <XCTest/XCTest.h>
#import "ScalarMockAboutWindowController.h"
#import "ScalarMockProductInfoFetcher.h"

NSString * const ExpectedGitVersionString = @"2.20.1.vfs.1.1.104.g2ab7360";
NSString * const ExpectedScalarVersionString = @"1.0.19116.1";

@interface ScalarAboutWindowControllerTests : XCTestCase

@property (strong) ScalarAboutWindowController *windowController;

@end

@implementation ScalarAboutWindowControllerTests

- (void)setUp
{
    [super setUp];
    
    ScalarMockProductInfoFetcher *mockProductInfoFetcher =
    [[ScalarMockProductInfoFetcher alloc] initWithGitVersion:(NSString *) ExpectedGitVersionString
                                         scalarVersion:(NSString *) ExpectedScalarVersionString];
    
    self.windowController = [[ScalarAboutWindowController alloc]
        initWithProductInfoFetcher:mockProductInfoFetcher];
}

- (void)tearDown
{
    [super tearDown];
}

- (void)testAboutWindowContainsScalarVersion
{
    XCTAssertEqual(self.windowController.scalarVersion,
                   ExpectedScalarVersionString,
                   @"Incorrect Scalar version displayed in About box");
}

- (void)testAboutWindowContainsGitVersion
{
    XCTAssertEqual(self.windowController.gitVersion,
                   ExpectedGitVersionString,
                   @"Incorrect Git version displayed in About box");
}

@end
