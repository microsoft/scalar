# Scalar Unit Tests

* Targets .NET Core
* Contains all unit tests that OS agnostic
* Conditionally includes the Windows\\** directory when running on Windows.

## Running Unit Tests

**Option 1: `dotnet test`**

`dotnet test` will run Scalar.UnitTests, and if on Windows also the Windows-only tests.

**Option 2: Run individual tests from Visual Studio**

Unit tests can both be run from Visual Studio. Simply open the Test Explorer (VS for Windows) or Unit Tests Pad (VS for Mac) and run or debug the tests you wish.

## Adding New Tests

Whenever possible new unit tests should be added to the root Scalar.UnitTests direcotry. If the new tests are specific to the Window OS then they will need to be added to the Scalar.UnitTests\\Windows directory.
