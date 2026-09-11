# Advanced Process Injector

Zaawansowane, nowoczesne narzędzie napisane w języku **C# (WPF)** służące do zarządzania procesami systemowymi, diagnostyki oraz wstrzykiwania bibliotek DLL z obsługą dynamicznych motywów (jasny/ciemny).

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-512BD4?style=for-the-badge&logo=windows&logoColor=white)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

---

## Funkcje aplikacji

* **Zarządzanie procesami:** Podgląd listy uruchomionych procesów wraz z ich ikonami, identyfikatorem (PID) oraz zużyciem pamięci RAM.
* **Wyszukiwarka w czasie rzeczywistym:** Szybkie filtrowanie procesów po nazwie lub numerze PID.
* **Auto-odświeżanie:** Opcja automatycznego odświeżania listy procesów co 3 sekundy.
* **Wiele metod wstrzykiwania:** Wybór spośród technik takich jak *Standard Injection*, *Thread Hijacking*, *LdrLoadDll Stub* oraz *Manual Map*.
* **Konsola diagnostyczna:** Wbudowany system logowania zdarzeń z dokładnymi znacznikami czasowymi.
* **Zarządzanie krytyczne:** Bezpieczne narzędzie do siłowego zamykania zablokowanych procesów.
* **Dynamiczne motywy:** Pełne wsparcie dla trybu ciemnego oraz jasnego.

---

## Wymagania systemowe

* System operacyjny: **Windows 10 / 11** (x64)
* Środowisko uruchomieniowe: **.NET** (wersja zgodna z projektem WPF)
* Uprawnienia: **Administrator** (wymagane do otwierania uchwytów i wstrzykiwania kodu do procesów systemowych).

---

## Pobieranie i uruchomienie (Dla użytkowników)

1. Przejdź do zakładki **[Releases](../../releases)** tego repozytorium.
2. Pobierz najnowszą skompilowaną paczkę `.zip`.
3. Rozpakuj archiwum i uruchom plik `ModernInjector.exe`.

---

## Kompilacja ze źródeł (Dla deweloperów)

Jeśli chcesz samodzielnie zmodyfikować lub skompilować kod:

1. Sklonuj repozytorium lub pobierz je jako ZIP:
   ```bash
   git clone [https://github.com/HMpower1/ModernInjector.git](https://github.com/HMpower1/ModernInjector.git)