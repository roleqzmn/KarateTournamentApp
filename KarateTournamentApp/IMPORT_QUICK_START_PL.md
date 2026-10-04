# Import Zawodników - Szybki Start

## Dostępne Formaty

Aplikacja obsługuje import zawodników z plików Excel (.xlsx, .xls).

- Najwygodniejszy dla ręcznej edycji
- Znany interfejs (Microsoft Excel, LibreOffice, Google Sheets)
- Łatwa walidacja danych
- Zawiera arkusz z instrukcjami

---

## Jak Zacząć?

### Krok 1: Wygeneruj Szablon
W aplikacji:
1. Otwórz menu boczne (przycisk ☰)
2. Kliknij **"Szablon Excel"**
3. Zapisz plik w wybranej lokalizacji

### Krok 2: Wypełnij Dane
Otwórz wygenerowany plik i dodaj zawodników zgodnie z szablonem.

### Krok 3: Ustaw Opcje Podziału
W aplikacji zaznacz:
- **"Podział według wieku"** LUB
- **"Podział według pasów"**

(Możesz zaznaczyć obie opcje)

### Krok 4: Importuj
1. Kliknij **"Import Excel"**
2. Wybierz swój plik
3. Gotowe! Zawodnicy zostaną automatycznie przypisani do kategorii

---

## Wymagane Dane

Dla każdego zawodnika musisz podać:
- Imię (FirstName)
- Nazwisko (LastName)
- Wiek (Age) - liczba całkowita
- Pas (Belt) - np. Kyu5, Dan
- Płeć (Sex) - Male, Female, Unisex
- Kategorie (Categories) - minimum jedna

Opcjonalnie:
- Klub (Club)

---

## Możliwe Wartości

### Pasy (Belt):
`Kyu10`, `Kyu9`, `Kyu8`, `Kyu7`, `Kyu6`, `Kyu5`, `Kyu4`, `Kyu3`, `Kyu2`, `Kyu1`, `Dan`

### Płeć (Sex):
- `Male` - Mężczyzna
- `Female` - Kobieta
- `Unisex` - Mieszane

### Kategorie (Category):
- `Kata` - Kata
- `Kumite` - Kumite
- `Kihon` - Kihon
- `KobudoShort` - Kobudo (krótka broń)
- `KobudoLong` - Kobudo (długa broń)
- `Grappling` - Grappling

---

## Ważne Informacje

### Walidacja Danych
System automatycznie:
- Pomija wpisy z niekompletnymi danymi
- Wykrywa duplikaty (to samo imię i nazwisko)
- Sprawdza poprawność wartości
- Wymaga minimum jednej kategorii

### Po Imporcie
Zobaczysz komunikat z informacją:
- Liczba pomyślnie zaimportowanych zawodników
- Liczba pominiętych wpisów
- Powód pominięcia (duplikaty/błędne dane)

---

## Szczegółowa Dokumentacja

- **Excel Import**: Zobacz [EXCEL_IMPORT_README.md](EXCEL_IMPORT_README.md)

---

## Wskazówki

1. **Najpierw wygeneruj szablon** - zawiera przykładowe dane
2. **Sprawdź nagłówki** - muszą być poprawne
3. **Testuj na małej liczbie** - zaimportuj 2-3 zawodników na start
4. **Zachowaj kopię** - zawsze miej backup pliku źródłowego
5. **Użyj walidacji Excel** - pomaga uniknąć błędów przy wpisywaniu

---

## Problemy?

### Import się nie udaje?
- Sprawdź czy zaznaczono "Podział według wieku" lub "Podział według pasów"
- Upewnij się, że wartości są poprawne (Belt, Sex, Category)
- Sprawdź czy wiek jest liczbą całkowitą > 0

### Niektórzy zawodnicy są pomijani?
- Sprawdź czy nie ma duplikatów (to samo imię i nazwisko)
- Upewnij się, że wszystkie wymagane pola są wypełnione
- Sprawdź czy zawodnik ma przypisaną przynajmniej jedną kategorię

### Potrzebujesz pomocy?
Zobacz dokumentację importu Excel lub wygeneruj szablon z przykładowymi danymi.
