# 🚀 OmniDeck Dashboard

**OmniDeck** je moderan, lagan i sveobuhvatan sustav upravljanja za vaše Windows računalo. Dizajniran je s ciljem da gejmerima i power-userima omogući brzi pregled performansi sustava, pokretanje i organizaciju igara te brzu instalaciju najbitnijih aplikacija i alata — sve unutar jednog elegantnog i intuitivnog sučelja.

---

## ✨ Ključne Značajke

* **📊 Sustav za praćenje performansi (Hardware Monitor):**
  * Prikaz CPU opterećenja u stvarnom vremenu.
  * Praćenje iskorištenosti RAM memorije.
  * Prikaz GPU zauzeća.
  * Ugrađeni sat i datum za brz pregled.

* **🎮 Integrirana Steam Biblioteka:**
  * Automatsko skeniranje instaliranih Steam igara (podrška za više diskova/particija putem `libraryfolders.vdf`).
  * Pokretanje igara jednim klikom direktno putem Steam protokola.
  * Podrška za brzi pristup vanjskim launcherima (npr. Epic Games Launcher).

* **📦 Download Apps (Brza Instalacija Aplikacija):**
  * Automatsko preuzimanje i instalacija popularnih aplikacija u pozadini putem **Windows Package Manager-a (`winget`)**.
  * Podržani softver: **Discord, Steam, Google Chrome, Brave, Spotify, VS Code**.
  * Izravne poveznice na službene web stranice za ručnu instalaciju.

* **🛠️ Sistemski i Power-User Alati:**
  * Brzi pristup sustavskim alatima: Task Manager, Kalkulator, Notepad, Command Prompt (CMD) i Windows Settings.
  * Ugrađena integracija za pokretanje **Chris Titus Tech (CTT) Windows Utility** skripte za optimizaciju Windowsa.
  * Sigurne funkcije za brzi Restart i Shutdown računala.

---

## 🛠️ Tehnologije

* **C# / .NET 8.0**
* **WPF (Windows Presentation Foundation)** — za moderan, tamni korisnički sučelje (Dark Theme).
* **Windows Package Manager (`winget`)** — za pozadinsku instalaciju programa.
* **System.Diagnostics (Performance Counters)** — za očitavanje sistemskih resursa.

---

## 🚀 Instalacija i Pokretanje

### Opcija 1: Gotova Aplikacija (Za Korisnike)
1. Idi na sekciju **[Releases](../../releases)** s desne strane ovog repozitorija.
2. Preuzmi najnoviju `.zip` arhivu (npr. `OmniDeck-v1.0.zip`).
3. Raspakiraj arhivu na željeno mjesto.
4. Pokreni `OmniDeck.exe`.

### Opcija 2: Izvorne Datoteke (Za Developere)
Za preuzimanje i kompajliranje koda s računala potrebni su vam **.NET 8.0 SDK** i **Git**.

```bash
# Kloni repozitorij
git clone [https://github.com/Adnanexe/OmniDeck.git](https://github.com/Adnanexe/OmniDeck.git)

# Uđi u mapu projekta
cd OmniDeck

# Pokreni aplikaciju
dotnet run