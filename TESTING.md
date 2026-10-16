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

## Osobna weryfikacja integracyjna

Testy jednostkowe nie potwierdzają transakcji PostgreSQL, ograniczeń kluczy obcych,
odpowiedzi HTTP ani działania okien. Te scenariusze wymagają osobnych testów
integracyjnych. Również losowanie większych drabinek i przejmowanie konkurencji
powinny dostać testy przy implementacji odpowiednich reguł.
