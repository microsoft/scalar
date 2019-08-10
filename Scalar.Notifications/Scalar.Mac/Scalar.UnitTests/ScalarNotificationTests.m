#import <XCTest/XCTest.h>
#import "ScalarNotification.h"

@interface ScalarNotificationTests : XCTestCase
@end

@implementation ScalarNotificationTests

- (void)testCreateNotificationWithMissingIdFails
{
    NSDictionary *message = @{
                              @"Title" : @"foo",
                              @"Message" : @"bar",
                              @"Enlistment" : @"/foo/bar",
                              @"EnlistmentCount" : [NSNumber numberWithLong:0]
                              };
    
    NSError *error;
    ScalarNotification *notification;
    
    XCTAssertFalse([ScalarNotification tryValidateMessage:message
                                           buildNotification:&notification
                                                       error:&error]);
    XCTAssertNotNil(error);
}

- (void)testCreateNotificationWithInvalidIdFails
{
    NSDictionary *message = @{
                              @"Id" : [NSNumber numberWithLong:32],
                              @"Title" : @"foo",
                              @"Message" : @"bar",
                              @"EnlistmentCount" : [NSNumber numberWithLong:0]
                              };
    
    NSError *error;
    ScalarNotification *notification;
    XCTAssertFalse([ScalarNotification tryValidateMessage:message
                                           buildNotification:&notification
                                                       error:&error]);
    XCTAssertNotNil(error);
}

- (void)testCreateAutomountNotificationWithValidMessageSucceeds
{
    NSDictionary *message = @{
                              @"Id" : [NSNumber numberWithLong:0],
                              @"EnlistmentCount" : [NSNumber numberWithLong:5]
                              };
    
    NSError *error;
    ScalarNotification *notification;
    XCTAssertTrue([ScalarNotification tryValidateMessage:message
                                          buildNotification:&notification
                                                      error:&error]);
    XCTAssertTrue([notification.title isEqualToString:@"Scalar AutoMount"]);
    XCTAssertTrue([notification.message isEqualToString:@"Attempting to mount 5 Scalar repos(s)"]);
    XCTAssertNil(error);
}

- (void)testCreateMountNotificationWithValidMessageSucceeds
{
    NSString *enlistment = @"/Users/foo/bar/foo.bar";
    NSDictionary *message = @{
                              @"Id" : [NSNumber numberWithLong:1],
                              @"Enlistment" : enlistment
                              };
    
    NSError *error;
    ScalarNotification *notification;
    XCTAssertTrue([ScalarNotification tryValidateMessage:message
                                          buildNotification:&notification
                                                      error:&error]);
    XCTAssertTrue([notification.title isEqualToString:@"Scalar AutoMount"]);
    XCTAssertTrue([notification.message containsString:enlistment]);
    XCTAssertNil(error);
}

- (void)testCreateMountNotificationWithMissingEnlistmentFails
{
    NSDictionary *message = @{
                              @"Id" : [NSNumber numberWithLong:1],
                              };
    
    NSError *error;
    ScalarNotification *notification;
    XCTAssertFalse([ScalarNotification tryValidateMessage:message
                                           buildNotification:&notification
                                                       error:&error]);
    XCTAssertNotNil(error);
}

@end
