# QUEEN'S COLONY — Soundtrack V2 (KODTU)

Bu paket, oyunun ekran görüntülerindeki **sonbahar / pixel art / karınca kolonisi / dalga savunması** yapısına göre hazırlanmış özgün procedural chiptune soundtrack'tir. Üçüncü taraf müzik/ses sample'ı içermez. Unity ile kullanılabilir. Oyun normalde sürekli aktif olduğu için ayrı bir ambience klasörü veya sürekli rüzgar/mağara uğultusu **yoktur**.

## Klasörler

- `Music/Colony_Playlist/`: 9 adet sakin/orta tempolu **normal oynanış** şarkısı; her birini sırayla veya shuffle ile oynatın. 02 ve 03 **V1 ile aynı dosyalardır**, değiştirilmedi.
- `Music/Main_Menu/`: 1 adet daha yavaş ana menü müziği.
- `Music/Waves_And_Events/`: 5 saniyelik warning, 2 farklı savaş parçası, wave kazanma jingle, queen danger ve game over.
- `SFX_Optional/`: Önceki paketin 28 kısa eylem sesinin birebir kopyası. Bu klasörü kullanmak zorunda değilsiniz; ambiyans değildir.
- `Unity/AutumnAntColonyMusicDirector.cs`: Koloni playlist'i, sonraki parçaya yumuşak geçiş, dalga uyarısı, savaş, zafer, menü, ölüm.
- `Previews/`: Yeni parçaların ve tam soundtrack'in hızlı dinleme MP3'leri.
- `manifest.json`: Parça adı, süre, tempo, dosya yolu.

## Şarkılar

| Sıra | Dosya | Kullanımı / ruh hâli |
| --- | --- | --- |
| 01 | `01_autumn_colony_loop.ogg` | Oynanış: hareketli dış dünya / sonbahar (V1) |
| 02 | `02_underground_colony_loop.ogg` | Oynanış: sakin yer altı çalışması (V1, kullanıcının beğendiği) |
| 03 | `03_larvae_nursery_loop.ogg` | Oynanış: larva bakımı; yumuşak, melodik (V1, kullanıcının beğendiği) |
| 09 | `09_acorn_storehouse_loop.ogg` | Oynanış: yiyecek toplama / depo, sıcak ve hareketli |
| 10 | `10_root_tunnels_loop.ogg` | Oynanış: tünel / kazı, vurmalı odunsu düzenleme |
| 11 | `11_fallen_leaves_loop.ogg` | Oynanış: ılımlı melankoli, sonbahar hissi |
| 12 | `12_foragers_march_loop.ogg` | Oynanış: asker/devriye ve işçi koşturmacası, savaş olmayan hızlı parça |
| 13 | `13_fungus_gardens_loop.ogg` | Oynanış: fungus garden, parlak kısa çan sesleri |
| 14 | `14_queen_and_cradles_loop.ogg` | Oynanış: kraliçe / nursery, daha sakin |
| 16 | `16_autumn_ant_colony_title_loop.ogg` | Ana menü, oyun içi normal playlist'e EKLEMEYİN |
| 04 | `04_wave_warning_exact_5s.wav` | Düşman gelmeden **tam 5 saniye önce**, 1 kez |
| 05 | `05_enemy_wave_loop.ogg` | Savaş müziği A, her wave loop |
| 15 | `15_beetle_raid_alternate_loop.ogg` | Savaş müziği B, her wave loop (alternatif) |
| 06 | `06_wave_victory_jingle.ogg` | Düşman dalgası temizlenince kısa başarı melodisi |
| 07 | `07_queen_in_danger_loop.ogg` | Kraliçenin kritik hasar aldığı tehdit anı |
| 08 | `08_colony_game_over.ogg` | Game over |

## Oyun mantığı

**Normal oynanış:** Surface/Underground tab'ına tıklayınca şarkı değişmez. Tek bir ortak koloni playlist'i sırayla devam eder. Her şarkı bittiğinde yaklaşık 0.9 saniye overlap/crossfade ile diğerini çalar. `shufflePlaylist = true` ise **shuffle bag**: bir turdaki 9 şarkı tekrarlanmadan çalar, son çalan aynı şarkı bir sonraki turun ilk şarkısı olamaz.

**Wave başlarken:** Oyun countdown'u **5 saniyeyi gösterdiği an** `BeginWaveWarning()` çağır. Uyarı dosyası WAV, süresi tam 5.000 sn / 32 kHz. Ardından `PlayScheduled` ile otomatik battle başlar. `OnWaveStarted()` çağrısını da düşman spawn anında yapmak güvenli (warning'i kaçırdıysan battle'a doğrudan girer).

**Wave bitince:** Son düşman öldüğünde `OnWaveCleared()` çağır. Kısa zafer melodisi çalar. Sonra o ana kadar ulaşılmış playlist'in **sıradaki şarkısı** başlar. Her wave'de ilk sakin müziğe dönmez.

**Kraliçe kritik:** `OnQueenInDanger()`; normale dönünce `OnQueenSafe()`. Game over: `OnGameOver()`. Ana menü: `EnterMainMenu()`; oyun başlayınca `StartColonyPlaylist()`.

## Unity kurulumu (sürükle-bırak)

1. `Music` klasörünü `Assets/Audio/` içine kopyala.
2. Boş bir GameObject oluştur: `MusicDirector`. Üzerine **3 adet AudioSource** ekle (`musicA`, `musicB`, `oneShots`). **Play On Awake OFF**, **Spatial Blend 0 (2D)**.
3. `Unity/AutumnAntColonyMusicDirector.cs` scriptini bu objeye ekle. Üç source'u sürükleyip bağla.
4. `Colony Tracks` array'ine `Music/Colony_Playlist/` içindeki **9 OGG şarkının tamamını** ekle. Menü ve savaş şarkılarını buraya ekleme.
5. `Main Menu Loop` -> `16_autumn_ant_colony_title_loop.ogg`.
6. `Wave Warning Exactly Five Seconds Wav` -> `04_wave_warning_exact_5s.wav` (**OGG DEĞİL**).
7. `Battle Tracks` array'ine `05_enemy_wave_loop.ogg` ve `15_beetle_raid_alternate_loop.ogg` ekle. `Wave Clear Jingle` / `Queen Danger Loop` / `Game Over Jingle` alanlarına kalanları ekle.
8. Müzik OGG: `Load Type = Compressed In Memory`, `Preload Audio Data = true`. Wave uyarısı WAV: `Decompress On Load`, `Preload Audio Data = true`. `AudioSource.loop` durumunu script kendisi ayarlar.
9. Giriş sahnesinde istersen `Start On Awake = false` ve menü açıldığında `EnterMainMenu()`. Oyun başlarken `StartColonyPlaylist()` çağır. Direkt gameplay sahnesinde `Start On Awake = true` kullanabilirsin.
10. Scene değişiminde playlist'in durmaması için `DontDestroyOnLoad(gameObject)` kullanacaksan aynı direktörden 2 tane oluşmadığından emin ol. Birden fazla sahnede singleton veya bir bootstrap sahnesi tercih et.

### Oyundan çağırma örneği

```csharp
// YOUR game logic — this code is illustrative; call each event only ONCE.
[SerializeField] private AutumnAntColonyMusicDirector music;

void OnFiveSecondsRemain() => music.BeginWaveWarning();
void OnEnemiesSpawn()       => music.OnWaveStarted();
void OnAllEnemiesKilled()   => music.OnWaveCleared();
void OnQueenCritical()      => music.OnQueenInDanger();
void OnGameLost()           => music.OnGameOver();
void OnOpenMainMenu()       => music.EnterMainMenu();
void OnNewGame()            => music.StartColonyPlaylist();
```

### Ses önerileri

- `Music Volume`: 0.50–0.58
- `Event Volume`: 0.75–0.85
- Koloni geçişi (crossfade): 0.8–1.2 sn
- Savaşta müzik, çatışma SFX'lerini bastırıyorsa mixer ile savaş müziğini 2–3 dB kısın.
- Çok sayıda asker aynı anda vurduğunda `enemy_hit` / `ant_attack` seslerine global cooldown koyun (örn. 0.05–0.10 sn).

### Notlar

- Müzik 8/16-bit esintili **sentetik** enstrümanlarla üretildi; doğrudan bir oyunun müziğini veya korumalı bir parçayı kopyalamaz.
- Parçalar kusursuz beat-synced ortak BPM'de değildir; listedeki geçişler **crossfade** ile yapılır. İstersen ileride hepsi aynı tempo/müzikal ölçüye getirilebilir.
- `SetGamePaused(bool)` AudioListener.pause kullanır; pause menüsünde müziğin de durması isteniyorsa çağır. Oyunun Time.timeScale=0 olması tek başına sesleri durdurmaz.
