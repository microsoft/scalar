# Scalar Unit Tests

## Unit Test Projects

### Scalar.UnitTests

* Targets .NET Core
* Contains all unit tests that are .NET Standard compliant

### Scalar.UnitTests.Windows

* Targets .NET Framework
* Contains all unit tests that depend on .NET Framework assemblies
* Has links (in the `NetCore` folder) to all of the unit tests in Scalar.UnitTests 

Scalar.UnitTests.Windows links in all of the tests from Scalar.UnitTests to ensure that they pass on both the .NET Core and .Net Framework platforms.

## Running Unit Tests

**Option 1: `Scripts\RunUnitTests.bat`**

`RunUnitTests.bat` will run both Scalar.UnitTests and Scalar.UnitTests.Windows

**Option 2: Run individual projects from Visual Studio**

Scalar.UnitTests and Scalar.UnitTests.Windows can both be run from Visual Studio.  Simply set either as the StartUp project and run them from the IDE.

## Adding New Tests

### Scalar.UnitTests or Scalar.UnitTests.Windows?

Whenever possible new unit tests should be added to Scalar.UnitTests. If the new tests are for a .NET Framework assembly (e.g. `Scalar.Platform.Windows`) 
then they will need to be added to Scalar.UnitTests.Windows.

### Adding New Test Files

When adding new test files, keep the following in mind:

* New test files that are added to Scalar.UnitTests will not appear in the `NetCore` folder of Scalar.UnitTests.Windows until the Scalar solution is reloaded.
* New test files that are meant to be run on both .NET platforms should be added to the **Scalar.UnitTests** project.
