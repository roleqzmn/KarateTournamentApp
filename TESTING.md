# Testy jednostkowe

Uruchamianie na Windows z SDK .NET 10:

```powershell
dotnet test .\KarateTournamentApp.sln
```

Testy nie wymagają PostgreSQL, Dockera ani uruchamiania okien WPF.

## Zakres

- `KarateTournament.Api.Tests`: walidacja żądań tworzenia zawodników i kategorii;
  wymagane pola, zakresy wieku, enumy, duplikaty i puste kolekcje.
- `KarateTournamentApp.Tests/JudgingScoreCalculatorTests`: dokładność dziesiętna,
  próg odrzucania ocen, powtarzające się ekstrema, deterministyczny wybór indeksów
  i zachowanie wejściowej kolekcji.
- `KarateTournamentApp.Tests/BracketTests`: wolny los, awans zwycięzcy do właściwej
  strony finału, zakończenie kategorii i inicjalizacja walki dwóch zawodników.
- `KarateTournamentApp.Tests/TournamentIdentityNormalizerTests`: wspólna tożsamość
  zawodnika, naprawa kolizji ID wraz z referencjami, odrzucanie brakujących lub
  niejednoznacznych referencji oraz powtarzalność normalizacji.

Testy nie zakładają konkretnych wartości nowo wygenerowanych ID ani kolejności
wykonania. Wewnętrzny normalizator jest dostępny dla zestawu testowego przez
`InternalsVisibleTo`; nie zmienia to publicznego API aplikacji.

Kolejna część testów obejmuje:

- `ParticipantImportParserTests`: wymagane pola, poprawność wieku, nazwy enumów
  niezależne od wielkości liter, dozwolone wartości liczbowe i odrzucanie wartości
  niezdefiniowanych. Nieprawidłowe dyscypliny są pomijane; bez żadnej poprawnej
  dyscypliny cały zawodnik jest odrzucany.
- `CategoryManagerTests`: grupowanie według wieku i pasa, zgodność płci,
  kategorie unisex, osobne dyscypliny, typ kategorii kumite oraz próg seniora.

Testy parsera ujawniły, że `Enum.TryParse` akceptuje także niezdefiniowane wartości
liczbowe. Parser sprawdza teraz dodatkowo `Enum.IsDefined`. Testy zachowują
obsługę poprawnych wartości liczbowych, w tym `Dan = 0` i `Kata = 0`.

`AsyncRelayCommandTests` sprawdza blokowanie ponownego wykonania podczas operacji,
odblokowanie po sukcesie i błędzie oraz respektowanie CanExecute w obu wariantach
komend. ExecuteAsync przekazuje wyjątek do kodu używającego await;
Execute (interfejs ICommand używany przez WPF) przekazuje go do onError.
TaskCompletionSource pozwala testowi kontrolować zakończenie operacji bez Sleep.
Timeout w WaitAsync jest zabezpieczeniem przed zawieszeniem testu, nie opóźnieniem.

`CategoryMergeTests` obejmuje dwa scenariusze scalenia: zawodników i drużyn.
Sprawdza deduplikację po ID, zachowanie istniejącego obiektu przy kolizji,
połączenie pasów i zakresów wieku, płeć oraz brak zmian w kategorii źródłowej.

Generowanie drabinki sprawdzamy dla 3 zawodników (wolny los) i 8 zawodników
(pełna obsada), w obu typach kategorii. Test nie zakłada kolejności losowania:
sprawdza rozmiar, kompletność i unikalność obsady pierwszej rundy oraz awans
z wolnego losu. Wykrył i zabezpiecza poprawkę nadmiarowych meczów w generatorach.

`LocalIdGeneratorTests` sprawdza unikalność dodatnich ID przy równoległym
generowaniu i rezerwację ID wczytanego z pliku. Starsza rezerwacja nie może
cofnąć licznika. Testy nie resetują statycznego stanu i nie wymagają kolejnych
wartości bez luk, więc inne równoległe testy mogą korzystać z tego samego licznika.
Test równoległy nie stanowi formalnego dowodu bezpieczeństwa współbieżności.

## Osobna weryfikacja integracyjna

`JsonServiceTests` to testy integracyjne komponentów aplikacji, umieszczone
w istniejącym projekcie testowym. Łączą serializer, rzeczywisty plik tymczasowy
i normalizację ID. Każdy test ma unikalną ścieżkę i usuwa plik po zakończeniu.
Sprawdzają wyniki i precyzję ocen, surowe noty, stan Shobu, drużyny, polskie znaki,
nadpisywanie pliku oraz odrzucanie nieprawidłowego JSON i niespójnych referencji.

Test surowych ocen ujawnił pomijanie pól ValueTuple przez serializer. IncludeFields
zachowuje teraz Item1 (oceny) i Item2 (ID zawodnika). Dawnych zapisów zawierających
już tylko puste obiekty nie da się na tej podstawie odtworzyć.

Obecny zestaw nie potwierdza transakcji PostgreSQL, ograniczeń kluczy obcych,
odpowiedzi HTTP ani działania okien. Te scenariusze wymagają osobnych testów
integracyjnych. Przejmowanie konkurencji wymaga osobnych testów przy implementacji.
