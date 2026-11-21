# Karate Tournament Manager

A Windows desktop application for managing karate tournaments, built with WPF and .NET 10.

[Polski](README_PL.md) · [API and PostgreSQL setup](KarateTournament.Api/README.md) · [Tests](TESTING.md)

## Overview

The same WPF application opens a separate window for the organizer or the judge. In a shared tournament, the organizer manages registration and competition rosters through an ASP.NET Core API backed by PostgreSQL. Judges claim competitions and score locally. The shared judging workflow supports Kumite (Shobu Sanbon), Kata, Kihon and individual Kobudo.

The file-based tournament is also available through the `local` role.

## Features

### Competition Management
- **Multiple Competition Types**: Support for Kata, Kumite, Kihon, Kobudo (Short/Long), and Grappling
- **Bracket System**: Automatic bracket generation for elimination tournaments
- **Individual Competitions**: Judge scoring system for Kata and other forms-based events
- **Live Scoring**: Real-time score tracking with public scoreboard display
- **Timer Management**: Built-in timer for Kumite matches with manual control

### Participant Management
- **Registration System**: Easy participant registration with detailed information
- **Category Assignment**: Automatic categorization by age, belt rank, or both
- **Excel** — Generate an import template and import a batch of participants with automatic category assignment
- **JSON** — Import and export in file-based mode

### Kumite Features
- **Senshu System**: Automatic tracking of first-point advantage
- **Overtime (Encho-sen)**: Configurable overtime rounds with automatic time addition
- **Penalty Tracking**: Real-time penalty management for both competitors
- **Score Display**: Large public scoreboard for audience viewing

### Judge Panel
- **Score Entry**: Quick score input with validation
- **Time Control**: Start, stop, reset, and manual time adjustment
- **Match Management**: Easy match completion and progression
- **Dual Display**: Separate judge control panel and public scoreboard

### Draw Resolution
- **Automatic Tiebreaking**: Rule-based system using highest/lowest judge scores
- **1v1 Resolution**: Interactive draw resolution for tied positions
- **Public Display**: Full-screen scoreboard showing competitors and results
- **Senshu Consideration**: Automatic winner determination based on first-point advantage

### Data Management
- **PostgreSQL** — Shared tournament registry, exclusive claims, handoff checkpoints and final results
- **JSON Storage** — File-based tournaments and local judging checkpoints
- **Excel Import**: Direct import from Excel spreadsheets
- **Templates**: Automatic generation of import templates
- **Async Operations**: Non-blocking I/O with loading indicators

## Technology Stack

- **Framework**: .NET 10
- **UI**: Windows Presentation Foundation (WPF)
- **Architecture**: MVVM (Model-View-ViewModel)
- **Async/Await**: Full asynchronous I/O operations
- **API and database** — ASP.NET Core, EF Core and PostgreSQL
- **JSON**: System.Text.Json for serialization
- **Excel**: EPPlus for Excel file handling

## System Requirements (recommended)

- Windows 10 or later
- .NET 10 Desktop Runtime
- 2GB RAM
- 50MB free disk space
- Display resolution: 1024x768 or higher (1920x1080 recommended for dual-monitor scoreboard setup)

## Installation

1. Download the latest release from the releases page
2. Extract the archive to your desired location
3. Run `KarateTournamentApp.exe`

## Building from Source

### Prerequisites
- .NET 10 SDK
- Git
- Docker Desktop in Linux container mode when running PostgreSQL locally or running API integration tests

### Steps
```powershell
git clone https://github.com/roleqzmn/KarateTournamentApp.git
cd KarateTournamentApp
dotnet build .\KarateTournamentApp.sln
```

### Run a shared tournament

Start PostgreSQL and the API using the [API setup guide](KarateTournament.Api/README.md#start-postgresql). For local development, WPF connects to `https://localhost:7086` by default. Use the matching API and WPF builds.

After building the solution, start the organizer and judge in separate PowerShell windows from the repository root. The first block opens the organizer.

```powershell
$env:KARATE_TOURNAMENT_ROLE = 'organizer'
dotnet run --project .\KarateTournamentApp --no-build
```

```powershell
$env:KARATE_TOURNAMENT_ROLE = 'judge'
dotnet run --project .\KarateTournamentApp --no-build
```

The default role is `judge`. For a standalone JSON tournament, use `local`. For multiple computers on the same network, configure the API address and the separate organizer and judge keys. [LAN configuration](KarateTournament.Api/README.md#api-access)

## Usage

### Organizer and judge

1. In **Zawodnicy**, select **Podział po dodaniu lub imporcie**. The default is **Wiek i pas** (age and belt). Use **Zapisz zawodnika w API** for one participant or **Wygeneruj szablon Excel** followed by **Importuj Excel** for a batch. The API adds new participants to eligible competitions or creates them when needed. [Excel format](KarateTournamentApp/EXCEL_IMPORT_README.md)
2. In **Konkurencje**, review the rosters and status. Use **Odśwież** to check immediately or wait for the next automatic refresh. Results become available under **Wyniki** once the judge submits them.
3. In the judge window, use **Pobierz turniej z API**, then **Przejmij i rozpocznij**. Only one station can claim each competition. After scoring, close the scoring panel and use **Wyślij wyniki** under **Sesje lokalne**.

### Hand off a competition

1. In the organizer's **Konkurencje** tab, select the active competition and click **Zwolnij do kontynuacji**. The current judging station closes the scoring panel and saves the latest progress to PostgreSQL before the API releases the claim.
2. Wait for **Do wznowienia**. If the station is offline or closed, the organizer sees **Oczekuje na zwolnienie** until the station reconnects and uploads its checkpoint.
3. On the same or another judging station, refresh with **Pobierz turniej z API** and claim the competition. Individual scores resume from the saved participant. Kumite restores the bracket, points, penalties and remaining time with the clock paused. [Handoff and emergency recovery](KarateTournament.Api/README.md#hand-off-a-competition)

### Category management in file-based mode

- **Merge Categories**: Combine under-populated categories
- **Remove Participants**: Remove individuals from specific categories
- **Delete Categories**: Remove entire categories if needed

### Shobu Sanbon Matches

1. **Starting a Match**
   - Click Start timer
   - Enter scores using +/- buttons
   - System tracks Senshu automatically

2. **During Match**
   - Use Stop to pause timer
   - Add/remove penalties as needed
   - Manual time adjustment available

3. **Ending Match**
   - Click "Zakoncz walke" (Finish Match)
   - System checks for draw and Senshu
   - Overtime prompt appears if needed

4. **Overtime**
   - Accept overtime dialog
   - 60 seconds added automatically
   - Senshu resets for new round

### Individual Competitions (Kata)

1. **Judge Scoring**
   - Enter scores from multiple judges
   - System removes highest and lowest scores
   - Final score calculated automatically

2. **Ranking**
   - Automatic ranking by final score
   - Tiebreaking using judge scores
   - 1v1 resolution for equal scores

3. **Results Display**
   - View final rankings
   - Publish the ranking to the API in a shared tournament or export to JSON in file-based mode

## Project Structure

- [`KarateTournamentApp`](KarateTournamentApp/KarateTournamentApp.csproj) contains both [`OrganizerWindow.xaml`](KarateTournamentApp/Views/OrganizerWindow.xaml) and [`JudgeWindow.xaml`](KarateTournamentApp/Views/JudgeWindow.xaml).
- [`KarateTournament.Api`](KarateTournament.Api/README.md) serves the shared tournament and persists it in PostgreSQL.
- [`KarateClassLibrary`](KarateClassLibrary/KarateClassLibrary.csproj) contains shared enums, API contracts and category assignment rules.

```
KarateTournamentApp/
├── Models/              # Data models
│   ├── Category.cs      # Competition categories
│   ├── Match.cs         # Match and ShobuSanbonMatch
│   ├── Participant.cs   # Competitor information
│   └── ParticipantResult.cs
├── ViewModels/          # MVVM ViewModels
│   ├── MainViewModel.cs
│   ├── CompetitionManagerViewModel.cs
│   ├── IndividualCompetitionManagerViewModel.cs
│   ├── ScoreboardViewModel.cs
│   └── AsyncRelayCommand.cs
├── Views/               # WPF Views
│   ├── MainWindow.xaml   # File-based mode
│   ├── OrganizerWindow.xaml
│   ├── JudgeWindow.xaml
│   ├── ScoreboardView.xaml
│   ├── ScoreboardJudge.xaml
│   └── DrawResolverView.xaml
├── Services/            # Business logic services
│   ├── CategoryManager.cs
│   ├── JsonService.cs
│   ├── ExcelImportService.cs
│   ├── ImportService.cs
│   └── ExportService.cs
└── Converters/          # XAML value converters
```

## Key Features Detail

### Async Operations
All file I/O operations are fully asynchronous:
- Non-blocking UI during import/export
- Loading indicators for user feedback
- Proper error handling with user messages

### Senshu System
Automatic first-point advantage tracking:
- Flags set when first point is scored
- Considered during draw resolution
- Resets properly during overtime

### Bracket System
Smart tournament bracket generation:
- Automatic bye assignment for odd participants
- Power-of-two bracket sizing
- Winner promotion through bracket levels

### Data Persistence
- Shared tournaments store participants, competitions, handoff checkpoints and final results in PostgreSQL.
- Judge stations save working progress locally and upload on handoff or final submission.
- File-based mode saves and loads tournament state as JSON.

## Configuration

### Default Settings
- Match Duration: 180 seconds (3 minutes)
- Overtime Duration: 60 seconds
- Senshu: Enabled by default
- Judge Score Range: 0-10

### Customization
Settings can be modified in the code:
- `ShobuSanbonMatch.TimeRemaining` - Default match time
- Overtime duration in `StartOvertime()` method
- Score validation ranges in judge ViewModels

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch
3. Make your changes with clear commit messages
4. Add tests if applicable
5. Submit a pull request

### Code Style
- Follow C# coding conventions
- Use meaningful variable names
- Add XML documentation for public APIs
- Keep methods focused and concise

## Troubleshooting

### Common Issues

**Problem**: Application won't start
- Solution: Ensure .NET 10 Desktop Runtime is installed

**Problem**: Import fails
- Solution: Check file format matches template structure

**Newly registered participants have no competition**

Verify that WPF points to the API built from the same revision. In **Konkurencje**, select **Wszystkie** and click **Odśwież**. Automatic assignment runs when a participant is registered or imported.

**Problem**: Scoreboard not displaying
- Solution: Verify match type is Shobu Sanbon

**Problem**: Timer not working
- Solution: Check if match is properly initialized

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Authors

- Main Developer - [roleqzmn](https://github.com/roleqzmn)

## Acknowledgments

- WPF Community for UI patterns
- EPPlus for Excel handling
- System.Text.Json team for serialization

## Version History

### Current Version
- Separate organizer and judge windows with a shared PostgreSQL tournament
- Automatic competition assignment after registration or Excel import
- Safe handoff and resumption of a claimed competition
- Async I/O operations
- Senshu and overtime support
- Draw resolution system
- Public scoreboard display
- Excel/JSON import/export

## Support

For bug reports and feature requests, please use the GitHub Issues page.


## Contact

Project Link: [https://github.com/roleqzmn/KarateTournametApp](https://github.com/roleqzmn/KarateTournamentApp)
