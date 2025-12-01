# Task Plan: Create FennecLabs.NuGet Project

## Todo Items

- [ ] Create new class library project FennecLabs.NuGet in src folder
- [ ] Copy NuGetService.cs and update namespace
- [ ] Copy FeedService.cs and update namespace
- [ ] Copy FeedConfiguration.cs and update namespace
- [ ] Copy ConfigurationManager.cs and update namespace
- [ ] Update project file with correct dependencies (NuGet.Protocol package)
- [ ] Update namespaces from Consc to FennecLabs.NuGet
- [ ] Add FennecLabs.NuGet project to the solution
- [ ] Test that the project builds successfully

## Review

### Summary of Changes

Successfully created the `FennecLabs.NuGet` project and ported all NuGet functionality from the `consc` project.

**Files Created:**
1. `src/FennecLabs.NuGet/FennecLabs.NuGet.csproj` - New class library project with NuGet.Protocol package reference
2. `src/FennecLabs.NuGet/NuGetService.cs` - Main service for NuGet operations (search, download, metadata, etc.)
3. `src/FennecLabs.NuGet/FeedService.cs` - Service for managing NuGet feeds
4. `src/FennecLabs.NuGet/FeedConfiguration.cs` - Configuration model for feeds
5. `src/FennecLabs.NuGet/ConfigurationManager.cs` - Manages application settings and feed configurations
6. `src/FennecLabs.NuGet/AppSettings.cs` - Application settings model
7. `src/FennecLabs.NuGet/PackageFileInfo.cs` - Model for package file information

**Key Changes:**
- Updated all namespaces from `Consc` to `FennecLabs.NuGet`
- Changed configuration directory from `.consc` to `.fennec` in `ConfigurationManager`
- Added `NuGet.Protocol` package reference (version 6.14.0)
- All files successfully ported with proper namespace updates
- Project added to solution and builds successfully

**Functionality Ported:**
- Package search
- Package version retrieval
- Package metadata retrieval
- Package download
- Package contents listing
- Package file extraction
- Feed management (add, remove, set default)
- Configuration management with JSON persistence

All tests pass and the solution builds without errors.

---

# Task Plan: Add HTML Report Option to Scorecard Command

## Todo Items

- [x] Add --report option to scorecard command
- [x] Create HTML report generator method that includes project info, dependency tree, and scorecard results
- [x] Save HTML report to file with timestamp
- [x] Test the report generation

## Review

### Summary of Changes

Successfully added a `--report` option to the `scorecard` command that generates a beautiful HTML report with scorecard results and dependency tree information.

**Files Modified:**
1. `src/FennecLabs.Cli/Program.cs` - Added `--report` option and HTML report generation functionality

**Key Changes:**
- Added `--report` (`-r`) option to the `scorecard` command
- Modified `GetScorecardsForProjectAsync` to accept a `generateReport` parameter
- Created `GenerateHtmlReportAsync` method that generates a comprehensive HTML report with:
  - Project information and generation timestamp
  - Summary statistics (total packages, packages with scorecards, average score, errors)
  - Complete dependency tree showing both top-level and transitive packages
  - Detailed scorecard results for each package with:
    - Package name and version
    - Repository information
    - Overall score with color-coded badges
    - Individual check results with scores
    - Error messages for packages that failed
  - Modern, responsive CSS styling with:
    - Color-coded score badges (excellent/good/fair/poor)
    - Hover effects on package items
    - Grid layout for summary statistics
    - Visual distinction between top-level and transitive packages
- Created helper methods:
  - `GeneratePackageHtml` - Generates HTML for individual packages in the dependency tree
  - `GetScoreClass` - Returns CSS class based on score value
  - `GetCheckScoreClass` - Returns CSS class for individual check scores
  - `EscapeHtml` - Escapes HTML special characters for safe rendering
- HTML report is saved with timestamp in filename: `scorecard-report-{timestamp}.html`
- Report is saved in the current working directory

**Features:**
- Beautiful, modern HTML design with responsive layout
- Color-coded score badges for quick visual assessment
- Complete dependency tree visualization
- Detailed scorecard information for each package
- Summary statistics at the top
- Error handling and display
- Timestamped report files

**Usage:**
```bash
# Generate scorecard report for current directory
dotnet run --project src/FennecLabs.Cli/Fennec.csproj -- scorecard --report

# Generate scorecard report for specific project
dotnet run --project src/FennecLabs.Cli/Fennec.csproj -- scorecard --project path/to/project.csproj --report
```

The solution builds successfully with no errors or warnings.
