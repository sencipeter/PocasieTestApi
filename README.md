# .NET Developer – Zadanie

## Popis

Vytvor samostatnu REST Web API sluzbu, ktora bude vracat aktualnu teplotu v mestach:

```
GET /api/temperature/{city}
```

## Dostupne mesta

- "bratislava"  
- "praha"  
- "budapest"  
- "vieden"  

Teplota je udavana v 2 desatinnych miestach.

---

## WeatherAPI

Pre ziskanie aktualnej teploty vyuzi API (WeatherAPI) dostupne na vzorovej fake URL:

```
GET https://nejakepocasie.net/{cityId}
```

### cityId mapovanie

- 1 => "bratislava"  
- 2 => "praha"  
- 3 => "budapest"  
- 4 => "vieden"  

### Mozne responses

- 200, 404, 5xx … atd  
- V pripade HTTP 200 vrati:

```json
{ 
  "temperatureC": 12.3, 
  "measuredAtUtc": "2026-02-13T08:55:00Z" 
}
```

---

## Obmedzenia a spravanie

- Teplota sa meni (je dostupna potencionalne nova hodnota z WeatherAPI) len 2x do dna, a to o  
  **9:00am a 16:00 UTC**
- Ako kazda externa sluzba, aj toto WeatherAPI ma svoje vypadky, t.j. treba zabezpecit, aby sa neprenasali do nasej aplikacie, t.j. aby aplikacia vracala vzdy posledne vratenu/nameranu hodnotu z WeatherAPI
- Aplikacia bude nasadena vo viacero instanciach a znacne vytazovana pre ziskanie poslednej teploty jednotlivych podporovanych miest.

---

## API

Aplikacia bude spristupnena ako API, preto je dolezite poskytnut standardizovanu dokumentaciu (napr. Swagger) a zabezpecnie pomocou Bearer Tokenu.

---

## Logovanie

A radi vieme co sa deje / nedeje, takze potrebujeme logovat.

---

## Testy

Testy su optional.

---

## Deployment

Pouzivame k8s a docker.
