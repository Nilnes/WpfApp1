# WpfApp1: Robotin tilanseurantajärjestelmä

WpfApp1 on WPF-sovellus, joka seuraa teollisuusrobotin tilaa reaaliajassa. Sovellus kommunikoi ABB RobotStudion kanssa RWS-rajapinnan kautta (HTTP/JSON) ja tallentaa kerätyn datan SQL Server LocalDB -tietokantaan historiatarkastelua varten.

## Sisällys
- [Mitä sovellus tekee](#mitä-sovellus-tekee)
- [Miten se toimii](#miten-se-toimii)
- [Teknologiat](#teknologiat)
- [Esivaatimukset](#esivaatimukset)
- [Näin pääset alkuun](#näin-pääset-alkuun)
- [Tietokannan valmistelu](#tietokannan-valmistelu)
- [Asetukset ja konfigurointi](#asetukset-ja-konfigurointi)
- [Käyttö](#käyttö)
- [Vianhaku](#vianhaku)
- [Huomioita ja jatkokehitysideoita](#huomioita-ja-jatkokehitysideoita)

---

## Mitä sovellus tekee

Sovelluksen avulla voit seurata esimerkiksi:
- Moottorien ja ohjelman tila: onko robotti käynnissä vai pysähdyksissä
- Tarttujan tila: onko koura auki vai kiinni
- TCP-nopeus: työkalun keskipisteen nopeus
- Sijaintitiedot: reaaliaikaiset XYZ-koordinaatit
- Automaattinen tallennus: mittaukset tallentuvat kerran sekunnissa paikalliseen tietokantaan historiadataa varten

---

## Miten se toimii

Sovellus suorittaa jatkuvaa päivityssykliä (esim. 1 Hz):

1. Hakee tilatiedot RobotStudion RWS-endpointeista (HTTP/JSON)
2. Päivittää käyttöliittymän (kentät, listat ja mahdollinen trendikuvaaja)
3. Tallentaa mittauksen tietokantaan aikaleiman kanssa

Trendikuvaaja voidaan piirtää WPF:n Polyline-elementillä: arvot skaalataan piirtoalueen koordinaatteihin (min/max -> ruudun koko).

---

## Teknologiat

- Käyttöliittymä: .NET 8 + WPF (C#)
- Rajapinta: ABB RobotStudio RWS (HTTP/JSON)
- Tietokanta: Microsoft SQL Server LocalDB

---

## Esivaatimukset

Varmista, että koneeltasi löytyy:
- Visual Studio 2022 (tai uudempi) ja .NET 8 SDK
- SQL Server LocalDB (yleensä Visual Studion mukana)
- ABB RobotStudio, jossa RWS-rajapinta on aktiivinen

---

## Näin pääset alkuun

### 1) Projektin avaaminen
1. Kloonaa repo tai lataa projektitiedostot
2. Avaa `WpfApp1.sln` Visual Studiossa

### 2) RobotStudion tarkistus
- Varmista, että RobotStudio on käynnissä
- Varmista, että RWS on käytössä
- Sovellus voi olettaa robotin löytyvän osoitteesta `http://127.0.0.1:8081` (muuta tämä, jos ympäristösi on eri)

### 3) Käynnistä sovellus
- Paina F5 ja tarkista, että data alkaa päivittyä käyttöliittymään

---

## Tietokannan valmistelu

Luo tarvittava tietokanta ja taulu suorittamalla seuraava SQL-skripti esimerkiksi SQL Server Management Studiossa (SSMS) tai Visual Studion SQL-työkaluissa:

```sql
-- Luodaan tietokanta, jos sitä ei ole vielä olemassa
IF DB_ID('TeollinenInternetDB') IS NULL
    CREATE DATABASE TeollinenInternetDB;
GO

USE TeollinenInternetDB;
GO

-- Luodaan taulu mittauksille
IF OBJECT_ID('dbo.measurements', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.measurements
    (
        id INT IDENTITY(1,1) PRIMARY KEY,
        measured_at DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        gripper BIT NULL,
        tcp_speed FLOAT NULL,
        pos_x FLOAT NULL,
        pos_y FLOAT NULL,
        pos_z FLOAT NULL
    );
END
GO
```

---

## Asetukset ja konfigurointi

### RWS-endpoint / portti

Sovellus hakee dataa RobotStudion RWS-rajapinnan kautta. Jos robotti/RobotStudio ei ole oletusosoitteessa, muuta osoite sovelluksen asetuksiin/koodiin.

- Oletus (esimerkki): `http://127.0.0.1:8081`

Varmista myös, että RobotStudion asetukset ja palomuuri sallivat yhteyden.

### Connection String

Tarkista `MainWindow.xaml.cs` (tai se tiedosto/luokka, jossa tietokantayhteys tehdään), ja varmista että connection string osoittaa SQL Server LocalDB:hen.

Hyvä uutinen: jos joskus siirrytte “oikeaan” SQL Serveriin, yleensä riittää connection stringin vaihtaminen (koodi voi pysyä muuten samana).

### Päivitysnopeus (polling)

Jos päivitystahti (esim. 1 Hz) on kovakoodattu, sen voi myöhemmin siirtää konfiguraatioon. Tyypillisesti päivitys tehdään ajastimella ja UI päivitetään Dispatcher-kontekstissa.

---

## Käyttö

- Sovellus päivittää näkymän automaattisesti (esim. kerran sekunnissa)
- Viimeisimmät mittauspisteet näkyvät listassa / kentissä (toteutuksesta riippuen)
- Trendi näyttää mittausten kehityksen ajan suhteen (jos toteutettu)
- Mittaukset tallentuvat tietokantaan (aikaleima + arvot)

---

## Vianhaku

### “Ei saatavilla” / tyhjät arvot
- RobotStudio ei ole käynnissä
- RWS ei ole päällä
- Endpoint / portti on väärin (esim. 8081)
- RobotStudion asetukset eivät salli RWS-yhteyttä

### Sovellus kaatuu tai UI ei päivity
- Tarkista lokit / Visual Studion Output-ikkuna
- Varmista, että verkkohaut eivät blokkaa UI-säiettä (tarvittaessa taustasäie + Dispatcher UI-päivitykseen)

### Tietokantaan ei tallennu
- LocalDB puuttuu tai ei ole käynnissä
- Connection string on väärin
- Taulua ei ole luotu (aja SQL-skripti)
- Sovelluksella ei ole oikeuksia luoda/yhdistää tietokantaan (harvinaista LocalDB:llä, mutta mahdollista)

---

## Huomioita ja jatkokehitysideoita

Tämä on projektin ensimmäinen vaihe, ja siinä on muutamia tyypillisiä jatkokehityskohtia:

- Kovat koodatut asetukset -> erillinen konfiguraatio (esim. appsettings.json)
- Historiadatan näkymä -> suora haku tietokannasta ja suodatus aikavälillä
- Rinnakkaisuus -> datan haku taustalla, UI-päivitys Dispatcherilla
- Tekoäly apuna kehityksessä -> koodi on tuotettu osittain tekoälyn avulla ja sitä on käytetty erityisesti UI-ratkaisujen ja ongelmanratkaisun ideointiin
