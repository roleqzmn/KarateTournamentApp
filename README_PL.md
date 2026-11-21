# Karate Tournament Manager

Aplikacja Windows do zarządzania turniejami karate, zbudowana w WPF i .NET 10.

[English](README.md) · [Konfiguracja API i PostgreSQL](KarateTournament.Api/README.md) · [Testy](TESTING.md)

## Przegląd

Jedna aplikacja WPF otwiera osobne okno organizatora lub sędziego. We wspólnym turnieju organizator prowadzi rejestr i zarządza składami przez API z bazą PostgreSQL. Sędziowie przejmują konkurencje i oceniają lokalnie. Tryb sieciowy obsługuje sędziowanie Kumite (Shobu Sanbon), Kata, Kihon i indywidualnego Kobudo.

Tryb plikowy jest dostępny po ustawieniu roli `local`.

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
- **Excel** — generowanie szablonu i import zawodników z automatycznym przydziałem do konkurencji
- **JSON** — import i eksport w trybie plikowym

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
- **PostgreSQL** — wspólny rejestr turnieju, blokady konkurencji, zapis przy przekazaniu i wyniki końcowe
- **JSON** — zapis turnieju plikowego i roboczego postępu na stanowisku sędziego
- **Import Excel**: Bezpośredni import z arkuszy Excel
- **Szablony**: Automatyczne generowanie szablonów importu
- **Operacje Asynchroniczne**: Nieblokujące operacje I/O z wskaźnikami ładowania

## Stos Technologiczny

- **Framework**: .NET 10
- **UI**: Windows Presentation Foundation (WPF)
- **Architektura**: MVVM (Model-View-ViewModel)
- **Async/Await**: Pełne asynchroniczne operacje I/O
- **API i baza** — ASP.NET Core, EF Core i PostgreSQL
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
- .NET 10 SDK
- Git
- Docker Desktop w trybie kontenerów Linux do lokalnego PostgreSQL i testów integracyjnych API

### Kroki
```powershell
git clone https://github.com/roleqzmn/KarateTournamentApp.git
cd KarateTournamentApp
dotnet build .\KarateTournamentApp.sln
```

### Uruchomienie wspólnego turnieju

Uruchom PostgreSQL i API zgodnie z [instrukcją konfiguracji](KarateTournament.Api/README.md#start-postgresql). Domyślny adres używany przez WPF to `https://localhost:7086`. Zbuduj API i WPF z tej samej wersji repozytorium.

Po zbudowaniu rozwiązania użyj dwóch osobnych okien PowerShella w katalogu repozytorium. Pierwszy blok uruchamia organizatora.

```powershell
$env:KARATE_TOURNAMENT_ROLE = 'organizer'
dotnet run --project .\KarateTournamentApp --no-build
```

```powershell
$env:KARATE_TOURNAMENT_ROLE = 'judge'
dotnet run --project .\KarateTournamentApp --no-build
```

Domyślna rola to `judge`. Ustaw `local`, żeby pracować z turniejem w plikach JSON. Na osobnych komputerach skonfiguruj adres API i osobne klucze dla organizatora i sędziów. [Konfiguracja sieciowa](KarateTournament.Api/README.md#api-access)

## Użytkowanie

### Organizator i sędzia

1. W zakładce **Zawodnicy** ustaw **Podział po dodaniu lub imporcie**. Domyślnie jest **Wiek i pas**. Dodaj jedną osobę przez **Zapisz zawodnika w API** lub użyj **Wygeneruj szablon Excel** i **Importuj Excel**. API dopisuje nowych zawodników do pasujących konkurencji albo tworzy je w razie potrzeby. [Format Excela](KarateTournamentApp/EXCEL_IMPORT_README.md)
2. W zakładce **Konkurencje** sprawdź składy i statusy. Kliknij **Odśwież**, aby od razu pobrać aktualny stan. Po wysłaniu wyniku przez sędziego ranking otworzysz przyciskiem **Wyniki**.
3. W oknie sędziego kliknij **Pobierz turniej z API**, a potem **Przejmij i rozpocznij**. Konkurencję może przejąć jedno stanowisko naraz. Po zakończeniu zamknij panel oceniania i kliknij **Wyślij wyniki** w zakładce **Sesje lokalne**.

### Przekazanie konkurencji

1. Organizator wybiera trwającą konkurencję i klika **Zwolnij do kontynuacji**. Stanowisko sędziego zamyka panel oceniania, a API zapisuje bieżący stan w PostgreSQL i zwalnia blokadę.
2. Poczekaj na status **Do wznowienia**. Jeśli stanowisko sędziego jest offline lub aplikacja jest zamknięta, status **Oczekuje na zwolnienie** pozostaje do ponownego połączenia i wysłania zapisu.
3. Ten sam lub inny sędzia klika **Pobierz turniej z API**, a następnie przejmuje konkurencję. W konkurencjach indywidualnych wracają noty i aktualny zawodnik. W Kumite wracają drabinka, punkty, kary i pozostały czas. Zegar startuje w pauzie. [Przekazanie i odzyskiwanie awaryjne](KarateTournament.Api/README.md#hand-off-a-competition)

### Zarządzanie kategoriami w trybie plikowym

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
   - Wyślij ranking do API we wspólnym turnieju lub wyeksportuj do JSON w trybie plikowym

## Struktura Projektu

- [`KarateTournamentApp`](KarateTournamentApp/KarateTournamentApp.csproj) zawiera [okno organizatora](KarateTournamentApp/Views/OrganizerWindow.xaml) i [okno sędziego](KarateTournamentApp/Views/JudgeWindow.xaml).
- [`KarateTournament.Api`](KarateTournament.Api/README.md) obsługuje wspólny turniej i zapis w PostgreSQL.
- [`KarateClassLibrary`](KarateClassLibrary/KarateClassLibrary.csproj) zawiera współdzielone enumy, kontrakty API i reguły przydziału do konkurencji.

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
│   ├── MainWindow.xaml   # Tryb plikowy
│   ├── OrganizerWindow.xaml
│   ├── JudgeWindow.xaml
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
- Wspólny turniej zapisuje zawodników, konkurencje, stan przekazania i wyniki w PostgreSQL.
- Stanowisko sędziego zapisuje postęp lokalnie i wysyła go przy przekazaniu albo publikacji wyniku końcowego.
- Tryb plikowy zapisuje i odczytuje turniej w formacie JSON.

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

**Nowo dodany zawodnik nie ma konkurencji**

Sprawdź, czy WPF łączy się z API uruchomionym z tej samej wersji repozytorium. W zakładce **Konkurencje** wybierz **Wszystkie** i kliknij **Odśwież**. Automatyczny przydział działa podczas rejestracji lub importu.

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
- Osobne okna organizatora i sędziego ze wspólną bazą PostgreSQL
- Automatyczny przydział do konkurencji po rejestracji lub imporcie Excela
- Przekazanie i wznowienie przejętej konkurencji
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
- Sieciowe wyświetlanie tablicy wyników
- Aplikacja towarzysząca na telefon
- Integracja nagrywania wideo
- Narzędzia analizy statystycznej

## Kontakt

Link do projektu — [KarateTournamentApp na GitHubie](https://github.com/roleqzmn/KarateTournamentApp).
