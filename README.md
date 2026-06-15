# Karate Tournament Manager

A comprehensive desktop application for managing karate tournaments, built with WPF and .NET 8.

## Overview

Karate Tournament Manager is a Windows desktop application designed to streamline the organization and management of karate competitions. It supports multiple competition formats including Kata (forms), Kihon (basics), Kobudo (weapons), and Grappling, with specialized features for Shobu Sanbon (point-based sparring) matches.

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
- **Import/Export**: Support for XML, Excel, and JSON formats
- **Duplicate Detection**: Automatic checking for duplicate participants

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
- **JSON Storage**: Tournament data persistence in JSON format
- **XML Import**: Bulk participant import from XML files
- **Excel Import**: Direct import from Excel spreadsheets
- **Templates**: Automatic generation of import templates
- **Async Operations**: Non-blocking I/O with loading indicators

## Technology Stack

- **Framework**: .NET 8
- **UI**: Windows Presentation Foundation (WPF)
- **Architecture**: MVVM (Model-View-ViewModel)
- **Async/Await**: Full asynchronous I/O operations
- **JSON**: System.Text.Json for serialization
- **Excel**: EPPlus for Excel file handling
- **XML**: LINQ to XML for XML parsing

## System Requirements (recommended)

- Windows 10 or later
- .NET 8 Runtime
- 2GB RAM
- 50MB free disk space
- Display resolution: 1024x768 or higher (1920x1080 recommended for dual-monitor scoreboard setup)

## Installation

1. Download the latest release from the releases page
2. Extract the archive to your desired location
3. Run `KarateTournamentApp.exe`

## Building from Source

### Prerequisites
- Visual Studio 2022 or later
- .NET 8 SDK
- Git

### Steps
```bash
git clone https://github.com/roleqzmn/KarateTournamentApp.git
cd KarateTournamentApp
dotnet restore
dotnet build
```

### Running
```bash
dotnet run --project KarateTournamentApp/KarateTournamentApp.csproj
```

## Usage

### Quick Start

1. **Add Participants**
   - Select division criteria (by age, belt, or both)
   - Enter participant details
   - Choose competition categories
   - Click "Add Participant"

2. **Import Participants**
   - Use "Import XML" or "Import Excel" from the side menu
   - Generate templates using "Szablon XML" or "Szablon Excel"
   - Fill in participant data
   - Import the completed file

3. **Initialize Competition**
   - Review participants in each category
   - Click "Losuj" (Draw) to generate brackets
   - Click "Start" to begin the competition

4. **Run Competition**
   - Judge panel opens for score entry
   - Public scoreboard displays automatically
   - Track scores, time, and penalties in real-time
   - System automatically handles match progression

### Category Management

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
   - Export results to JSON

## Project Structure

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
│   ├── MainWindow.xaml
│   ├── ScoreboardView.xaml
│   ├── ScoreboardJudge.xaml
│   └── DrawResolverView.xaml
├── Services/            # Business logic services
│   ├── CategoryManager.cs
│   ├── JsonService.cs
│   ├── XmlImportService.cs
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
Comprehensive save/load functionality:
- Full tournament state in JSON
- Category hierarchy preservation
- Match history tracking

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
- Solution: Ensure .NET 8 Runtime is installed

**Problem**: Import fails
- Solution: Check file format matches template structure

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
- Async I/O operations
- Senshu and overtime support
- Draw resolution system
- Public scoreboard display
- Excel/XML/JSON import/export

## Support

For bug reports and feature requests, please use the GitHub Issues page.


## Contact

Project Link: [https://github.com/roleqzmn/KarateTournametApp](https://github.com/roleqzmn/KarateTournametApp)
