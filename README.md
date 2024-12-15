# music-sidescroller

Ez a tárhely a diplomatervemhez tartozik - "Zene szintézis számítógépes játékokhoz" (2024).

Megtalálható itt minden hozzátartozó kód, és a Releases fülön egy elkészült build Windows-ra. Az alkalmazás cross-platform, de csak Windows-on volt tesztelve.

## Telepítés

- Exe artifakt letöltése a Releases alól, majd annak kicsomagolása

*VAGY*

- Követelmény: Unity 2022
- Kód letöltése (`git clone -b music-sidescroller https://github.com/NomarCub/music-sidescroller`)
- Opcionális: futtatás a Unity szerkesztőből
- Opcionális: build készítése a Unity szerkesztőben a `File` > `Build Settings` alatt

### GETMusic (opcionális)

Követelmények: Python 3.10.

GETMusic kód letöltése a saját módosításaimmal: [link](https://github.com/NomarCub/muzic/tree/music-sidescroller).

```sh
git clone -b music-sidescroller https://github.com/NomarCub/muzic
```

GETMusic beállítása a hivatalos leírás alapján: [README](https://github.com/NomarCub/muzic/tree/music-sidescroller/getmusic#getmusic).

## Futtatás

`music-sidescroller.exe` elindítása dupla kattintással, vagy a parancssorból.

MIDI formátumban saját dal megadható a `--custom-song` parancssori kapcsolóval:

```sh
music-sidescroller.exe --custom-song my_folder/song.mid
```

GETMusic generálás bekapcsolása a `--getmusic-folder` és `--getmusic-checkpoint` kapcsolókkal:

```sh
music-sidescroller.exe --getmusic-folder my_folder/muzic/getmusic --getmusic-checkpoint my_folder/checkpoint.pth
```

### Irányítás

A WASD gombokkal mozgatható a játékos karakter, ebből az S segítségével guggol. Space gombra ugrik.

A rétegek között a Q és E gombokkal tud váltani hátra és előre, és a bal Shift gombbal tud váltani a segítő rétegre (vagy arról vissza), ha a zenei platformokon nem tudna túljutni.

#### Játékcél, pontozás

A játék célja a pályán minél magasabb pontszám elérése.

Pont jár:

- a zene előrehaladtával túlélésért (1 / másodperc)
- zenei (nem fehér) platformokra ugrásért első alkalommal
  - a platform létrejöttekor ad a legtöbb pontot (20), idővel ennél csak kevesebbet (minimum 5)
  - 2, vagy többszörös pont jár egy platformért, ha az előző pontot egy másik rétegben szerezte, vagy az elmúlt 10 másodpercben volt ilyen pontszerzés (3, vagy többszörös, ha még több rétegben)

## Tesztelés

A [BackendTests assembly](./Assets/Tests/Backend/) alatt elérhetők tesztek, amik a projekt Unity mentes kódrészét képesek tesztelni, Rider segítségével akár Unity nélkül.
Ehhez az [asmdef](./Assets/Tests/Backend/BackendTests.asmdef) fájlban ki kell egészíteni a hivatkozott platformokat a Unity Editorral (`"includePlatforms": ["Editor"]`).

## Zenék, amiket a játék alapból tartalmaz, demonstrálási céllal

01. [Title Screen (Evoland II) - Shakkam](https://musescore.com/user/12077776/scores/3581556)
02. [Ghost Forest Theme (Evoland II) - Shakkam](https://musescore.com/user/10949326/scores/6327036)
03. [God Rest Ye Merry Gentlemen](https://musescore.com/georgewu/scores/2979086)
04. [Who Can It Be Now - Men at Work](https://musescore.com/user/30237975/scores/5695163)
05. [János, legyen](https://musescore.com/kovianyo/scores/4920850)
06. [Karen and Hikari (Starlight Revue) - Yoshiaki Fujisawa](https://musescore.com/user/13229721/scores/5353388)
07. [Spiritual State - Nujabes](https://musescore.com/user/20554696/scores/6695220)
08. [Whiplash - Hank Levy](https://musescore.com/user/178085/scores/757001)
09. [In the Flame (Pyre) - Darren Korb](https://musescore.com/user/6675226/scores/4865086)
10. [Merry-Go-Round of Life - Joe Hisaishi](https://musescore.com/user/7445686/scores/2851556)
