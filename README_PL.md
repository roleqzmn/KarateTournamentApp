# Karate Tournament Manager

Kompleksowa aplikacja desktopowa do zarządzania turniejami karate, zbudowana w WPF i .NET 10.

## Przegląd

Karate Tournament Manager to aplikacja desktopowa dla systemu Windows, zaprojektowana w celu usprawnienia organizacji i zarządzania zawodami karate. Obsługuje wiele formatów konkurencji, w tym Kata (formy), Kumite (walki), Kihon (podstawy), Kobudo (broń) i Grappling, ze specjalnymi funkcjami dla walk Shobu Sanbon (walki punktowe).

## Funkcje

### Zarządzanie Konkurencjami
- **Wiele Typów Konkurencji**: Obsługa Kata, Kumite, Kihon, Kobudo (Krótka/Długa), i Grappling
- **System Drabinek**: Automatyczne generowanie drabinek dla turniejów eliminacyjnych
- **Konkurencje Indywidualne**: System oceniania sędziowskiego dla Kata i innych konkurencji formowych
- **Wyniki na Żywo**: Śledzenie wyników w czasie rzeczywistym z publiczną tablicą wyników
- **Zarządzanie Czasem**: Wbudowany timer dla walk Shobu Sanbon z ręczną kontrolą

### Zarządzanie Zawodnikami
- **System Rejestracji**: Łatwa rejestracja zawodników ze szczegółowymi informacjami
- **Przypisywanie Kategorii**: Automatyczna kategoryzacja według wieku, pasa lub obu
- **Import/Export**: Obsługa formatów Excel i JSON
- **Wykrywanie Duplikatów**: Automatyczne sprawdzanie duplikatów zawodników

### Funkcje Shobu Sanbon
- **System Senshu**: Automatyczne śledzenie przewagi pierwszego punktu
- **Dogrywka (Encho-sen)**: Konfigurowalne rundy dogrywek z automatycznym dodawaniem czasu
- **Śledzenie Kar**: Zarządzanie karami w czasie rzeczywistym dla obu zawodników
- **Wyświetlanie Wyników**: Duża publiczna tablica wyników dla widowni

### Panel Sędziowski
- **Wprowadzanie Wyników**: Szybkie wprowadzanie wyników z walidacją
- **Kontrola Czasu**: Start, stop, reset i ręczna regulacja czasu
- **Zarządzanie Walkami**: Łatwe kończenie walk i przechodzenie do kolejnych
- **Podwójny Wyświetlacz**: Oddzielny panel kontrolny sędziego i publiczna tablica wyników

### Rozstrzyganie Remisów
- **Automatyczne Rozstrzyganie**: System oparty na regułach używający najwyższych/najniższych ocen sędziów
- **Rozstrzyganie 1v1**: Interaktywne rozstrzyganie remisów dla remisowych pozycji
- **Wyświetlacz Publiczny**: Pełnoekranowa tablica wyników pokazująca zawodników i wyniki
- **Uwzględnienie Senshu**: Automatyczne określanie zwycięzcy na podstawie przewagi pierwszego punktu

### Zarządzanie Danymi
- **Przechowywanie JSON**: Trwałość danych turnieju w formacie JSON
- **Import Excel**: Bezpośredni import z arkuszy Excel
- **Szablony**: Automatyczne generowanie szablonów importu
- **Operacje Asynchroniczne**: Nieblokujące operacje I/O z wskaźnikami ładowania

## Stos Technologiczny

- **Framework**: .NET 10
- **UI**: Windows Presentation Foundation (WPF)
- **Architektura**: MVVM (Model-View-ViewModel)
- **Async/Await**: Pełne asynchroniczne operacje I/O
- **JSON**: System.Text.Json do serializacji
- **Excel**: EPPlus do obsługi plików Excel

## Wymagania Systemowe (rekomendowane)

- Windows 10 lub nowszy
- .NET 10 Desktop Runtime
- Minimum 2GB RAM
- 50MB wolnej przestrzeni dyskowej
- Rozdzielczość ekranu: 1024x768 lub wyższa (1920x1080 zalecana dla konfiguracji z dwoma monitorami do tablicy wyników)

## Instalacja

1. Pobierz najnowszą wersję ze strony releases
2. Wypakuj archiwum do wybranej lokalizacji
3. Uruchom `KarateTournamentApp.exe`

## Budowanie ze Źródeł

### Wymagania
- Visual Studio 2022 lub nowszy
- .NET 10 SDK
- Git

### Kroki
```bash
git clone https://github.com/roleqzmn/KarateTournametApp.git
cd KarateTournamentApp
dotnet restore
dotnet build
```

### Uruchamianie
```bash
dotnet run --project KarateTournamentApp/KarateTournamentApp.csproj
```

## Użytkowanie

### Szybki Start

1. **Dodawanie Zawodników**
   - Wybierz kryteria podziału (według wieku, pasa lub obu)
   - Wprowadź dane zawodnika
   - Wybierz kategorie konkurencji
   - Kliknij "Dodaj Zawodnika"

2. **Import Zawodników**
   - Użyj "Import Excel" z menu bocznego
   - Wygeneruj szablon używając "Szablon Excel"
   - Wypełnij dane zawodników
   - Importuj wypełniony plik

3. **Inicjalizacja Konkurencji**
   - Przejrzyj zawodników w każdej kategorii
   - Kliknij "Losuj" aby wygenerować drabinki
   - Kliknij "Start" aby rozpocząć konkurencję

4. **Prowadzenie Konkurencji**
   - Panel sędziowski otwiera się do wprowadzania wyników
   - Publiczna tablica wyników wyświetla się automatycznie
   - Śledź wyniki, czas i kary w czasie rzeczywistym
   - System automatycznie obsługuje przechodzenie między walkami

### Zarządzanie Kategoriami

- **Łączenie Kategorii**: Łączenie niedostatecznie zapełnionych kategorii
- **Usuwanie Zawodników**: Usuwanie osób z konkretnych kategorii
- **Usuwanie Kategorii**: Usuwanie całych kategorii w razie potrzeby

### Walki Shobu Sanbon

1. **Rozpoczynanie Walki**
   - Kliknij Start timer
   - Wprowadzaj punkty używając przycisków +/-
   - System śledzi Senshu automatycznie

2. **Podczas Walki**
   - Użyj Stop aby zatrzymać timer
   - Dodawaj/usuwaj kary według potrzeb
   - Dostępna ręczna regulacja czasu

3. **Kończenie Walki**
   - Kliknij "Zakoncz walke"
   - System sprawdza remis i Senshu
   - Pojawia się prompt o dogrywkę jeśli potrzebna

4. **Dogrywka**
   - Zaakceptuj dialog dogrywki
   - 60 sekund dodane automatycznie
   - Senshu resetuje się dla nowej rundy

### Konkurencje Indywidualne (Kata)

1. **Ocenianie Sędziowskie**
   - Wprowadź oceny od wielu sędziów
   - System usuwa najwyższą i najniższą ocenę
   - Wynik końcowy obliczany automatycznie

2. **Rankowanie**
   - Automatyczne rankowanie według wyniku końcowego
   - Rozstrzyganie remisów używając ocen sędziów
   - Rozstrzyganie 1v1 dla równych wyników

3. **Wyświetlanie Wyników**
   - Zobacz końcowe rankingi
   - Eksportuj wyniki do JSON

## Struktura Projektu

```
KarateTournamentApp/
├── Models/              # Modele danych
│   ├── Category.cs      # Kategorie konkurencji
│   ├── Match.cs         # Match i ShobuSanbonMatch
│   ├── Participant.cs   # Informacje o zawodniku
│   └── ParticipantResult.cs
├── ViewModels/          # MVVM ViewModels
│   ├── MainViewModel.cs
│   ├── CompetitionManagerViewModel.cs
│   ├── IndividualCompetitionManagerViewModel.cs
│   ├── ScoreboardViewModel.cs
│   └── AsyncRelayCommand.cs
├── Views/               # Widoki WPF
│   ├── MainWindow.xaml
│   ├── ScoreboardView.xaml
│   ├── ScoreboardJudge.xaml
│   └── DrawResolverView.xaml
├── Services/            # Serwisy logiki biznesowej
│   ├── CategoryManager.cs
│   ├── JsonService.cs
│   ├── ExcelImportService.cs
│   ├── ImportService.cs
│   └── ExportService.cs
└── Converters/          # Konwertery wartości XAML
```

## Szczegóły Kluczowych Funkcji

### Operacje Asynchroniczne
Wszystkie operacje I/O na plikach są w pełni asynchroniczne:
- Nieblokujące UI podczas importu/exportu
- Wskaźniki ładowania dla informacji użytkownika
- Właściwa obsługa błędów z komunikatami dla użytkownika

### System Senshu
Automatyczne śledzenie przewagi pierwszego punktu:
- Flagi ustawiane gdy pierwszy punkt zostanie zdobyty
- Uwzględniane podczas rozstrzygania remisu
- Prawidłowe resetowanie podczas dogrywki

### System Drabinek
Inteligentne generowanie drabinek turniejowych:
- Automatyczne przypisywanie "wolnych losów" dla nieparzystej liczby zawodników
- Rozmiar drabinki jako potęga dwójki
- Promocja zwycięzcy przez poziomy drabinki

### Trwałość Danych
Kompleksowa funkcjonalność zapisu/wczytywania:
- Pełny stan turnieju w JSON
- Zachowanie hierarchii kategorii
- Śledzenie historii walk

## Konfiguracja

### Ustawienia Domyślne
- Czas Walki: 180 sekund (3 minuty)
- Czas Dogrywki: 60 sekund
- Senshu: Włączone domyślnie
- Zakres Ocen Sędziów: 0-10

### Dostosowywanie
Ustawienia mogą być modyfikowane w kodzie:
- `ShobuSanbonMatch.TimeRemaining` - Domyślny czas walki
- Czas dogrywki w metodzie `StartOvertime()`
- Zakresy walidacji ocen w ViewModelach sędziowskich

## Współtworzenie

Wkład jest mile widziany! Proszę przestrzegać tych wytycznych:

1. Zforkuj repozytorium
2. Utwórz branch dla funkcji
3. Wprowadź zmiany z czytelnymi komunikatami commitów
4. Dodaj testy jeśli dotyczy
5. Wyślij pull request

### Styl Kodu
- Przestrzegaj konwencji kodowania C#
- Używaj znaczących nazw zmiennych
- Dodawaj dokumentację XML dla publicznych API
- Utrzymuj metody skoncentrowane i zwięzłe

## Rozwiązywanie Problemów

### Częste Problemy

**Problem**: Aplikacja nie uruchamia się
- Rozwiązanie: Upewnij się, że .NET 10 Desktop Runtime jest zainstalowany

**Problem**: Import nie działa
- Rozwiązanie: Sprawdź czy format pliku pasuje do struktury szablonu

**Problem**: Tablica wyników nie wyświetla się
- Rozwiązanie: Zweryfikuj czy typ walki to Shobu Sanbon

**Problem**: Timer nie działa
- Rozwiązanie: Sprawdź czy walka jest prawidłowo zainicjalizowana

## Licencja

Ten projekt jest licencjonowany na licencji MIT - zobacz plik LICENSE dla szczegółów.

## Autorzy

- Główny Deweloper - [roleqzmn](https://github.com/roleqzmn)

## Podziękowania

- Społeczność WPF za wzorce UI
- EPPlus za obsługę Excela
- Zespół System.Text.Json za serializację

## Historia Wersji

### Aktualna Wersja
- Asynchroniczne operacje I/O
- Obsługa Senshu i dogrywek
- System rozstrzygania remisów
- Publiczne wyświetlanie tablicy wyników
- Import/export Excel/JSON

## Wsparcie

Dla zgłoszeń błędów i próśb o funkcje, proszę używać strony GitHub Issues.

## Plan Rozwoju

Planowane przyszłe ulepszenia:
- Drabinki podwójnej eliminacji
- Format turnieju każdy z każdym
- Funkcjonalność drukowania dla drabinek i wyników
- Opcja backendu bazodanowego
- Sieciowe wyświetlanie tablicy wyników
- Aplikacja towarzysząca na telefon
- Integracja nagrywania wideo
- Narzędzia analizy statystycznej

## Kontakt

Link do Projektu: [https://github.com/roleqzmn/KarateTournametApp](https://github.com/roleqzmn/KarateTournametApp)
