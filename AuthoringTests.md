# Authoring Tests

## Functional Tests

#### Runnable functional test projects

- `Scalar.FunctionalTests`

`Scalar.FunctionalTests` is a .NET Core project and contains all cross-platform functional tests.

## Running the Functional Tests

The functional tests are built on NUnit 3, which is available as a set of NuGet packages.

### Windows

1. Build Scalar:
    
    **Option 1:** Open Scalar.sln in Visual Studio and build everything.
    
    **Option 2:** Run `Scripts\BuildScalarForWindows.bat` from the command line

2. Run the Scalar installer that was built in step 2.  This will ensure that Scalar will be able to find the correct version of the pre/post-command hooks. The installer will be placed in `BuildOutput\Scalar.Installer.Windows\bin\x64\<Debug or Release>`
3. Run the tests **with elevation**.  Elevation is required because the functional tests create and delete a test service.

   **Option 1:** Run the `Scalar.FunctionalTests` project from inside Visual Studio launched as Administrator.
   
   **Option 2:** Run `Scripts\RunFunctionalTests.bat` from CMD launched as Administrator.

#### Selecting Which Tests are Run

By default, the functional tests run on a single configuration.  Passing the `--full-suite` option runs all tests on all configurations.

### Mac

1. Build Scalar: `Scripts/Mac/BuildScalarForMac.sh`
2. Run the tests: `Scripts/Mac/RunFunctionalTests.sh `

If you need the VS for Mac debugger attached for a functional test run:

1. Make sure you've built your latest changes
2. Open Scalar.sln in VS for Mac
3. Run->Run With->Custom Configuration...
4. Select "Start external program" and specify the published functional test binary (e.g. `/Users/<USERNAME>/Repos/Scalar/Publish/Scalar.FunctionalTests`)
5. Specify any desired arguments (e.g. [a specific test](#Running-Specific-Tests) )
6. Run Action -> "Debug - .Net Core Debugger"
7. Click "Debug"

### Customizing the Functional Test Settings

The functional tests take a set of parameters that indicate what paths and URLs to work with.  If you want to customize those settings, they
can be found in [`Scalar.FunctionalTests\Settings.cs`](/Scalar/Scalar.FunctionalTests/Settings.cs).


## Running Specific Tests

Specific tests can be run by adding the `--test=<comma separated list of tests>` command line argument to the functional test project/scripts.  

Note that the test name must include the class and namespace and that `Debug` or `Release` must be specified when running the functional test scripts.

*Example*

Windows (Script):

`Scripts\RunFunctionalTests.bat Debug --test=Scalar.FunctionalTests.Tests.EnlistmentPerFixture.CloneTests.CloneToPathWithSpaces

Windows (Visual Studio):

1. Set `Scalar.FunctionalTests` as StartUp project
2. Project Properties->Debug->Start options->Command line arguments (all on a single line): `--test=Scalar.FunctionalTests.Tests.EnlistmentPerFixture.CloneTests.CloneToPathWithSpaces

Mac:

`Scripts/Mac/RunFunctionalTests.sh Debug --test=Scalar.FunctionalTests.Tests.EnlistmentPerFixture.CloneTests.CloneToPathWithSpaces`

## How to Write a Functional Test

Each piece of functionality that we add to Scalar should have corresponding functional tests that clone a repo and use existing tools and filesystem APIs to interact with the virtual repo.

Since these are functional tests that can potentially modify the state of files on disk, you need to be careful to make sure each test can run in a clean 
environment.  There are two base classes that you can derive from when writing your tests.  It's also important to put your new class into the same namespace
as the base class, because NUnit treats namespaces like test suites, and we have logic that keys off that for deciding when to create enlistments.

1. `TestsWithEnlistmentPerFixture`

    For any test fixture (a fixture is the same as a class in NUnit) that derives from this class, we create an enlistment before running any of the tests in the fixture, and then we delete the enlistment after all tests are done (but before any other fixture runs).  If you need to write a sequence of tests that manipulate the same repo, this is the right base class.

2. `TestsWithEnlistmentPerTestCase`

   Derive from this class if you need a new enlistment created for each test case.  This is the most reliable, but also most expensive option.

## Updating the Remote Test Branch

By default, the functional tests clone `main`, check out the branch "FunctionalTests/YYYYMMDD" (with the day the FunctionalTests branch was created), 
and then remove all remote tracking information. This is done to guarantee that remote changes to tip cannot break functional tests. If you need to update 
the functional tests to use a new FunctionalTests branch, you'll need to create a new "FunctionalTests/YYYYMMDD" branch and update the `Commitish` setting in `Settings.cs` to have this new branch name.  
Once you have verified your scenarios locally you can push the new FunctionalTests branch and then your changes.
