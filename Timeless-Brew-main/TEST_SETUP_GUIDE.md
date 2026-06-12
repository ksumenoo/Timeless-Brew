# Timeless Brew — проверка в Unity (тестовая сцена, диздок v4.3)

Тестовая сцена на примитивах: **(A) цикл дня** (гости/заказы/звёздная оценка/чаевые) и
**(B) полная готовка** строго по §4.2/§6 (засыпка+скилчек → обжарка → просеивание → помол →
перенос ложкой → варка → разлив → латте-арт → подача). Гости — капсулы, предметы — кубики.

Вся логика «принять/вылить/взять» — на ассетах состояний `ItemStateSO` (`Pickable / Can Receive / Can Pour`).

---

## 0. Подготовка (один раз)

### 0.1. Слои
`Project Settings → Tags and Layers`: **Item** (все предметы) и **Table** (поверхность стола).

### 0.2. Флаги существующих состояний (`Assets/Data/States/`)
- `JarFull` → Can Pour ✓
- `PanEmpty` → Can Receive ✓; `PanBeans`, `PanHot` → Can Pour ✓
- `StoveCold/Smolder/Even/Strong` → Pickable ✗
- Остальные состояния созданы с нужными флагами автоматически.

> Если остались от прежних версий — удали ассеты `GroundCoffeeFull`, `ColdWaterFull` и объекты
> `GroundCoffeeJar`, `ColdWaterJug` (они больше не используются: молотый носит ложка, холодная вода — из крана).

### 0.3. Input Actions
`Create → Input Actions` → `GameplayInput`, map **Gameplay**: **Point** (Value/Vector2, `<Mouse>/position`),
**Interact** (Button, `<Mouse>/leftButton`).

---

## Часть A — Цикл дня (без ввода)

| GameObject | Компонент | Заполнить |
|---|---|---|
| `DayManager` | `DayManager` | `Guests` = ассеты, `Auto Start On Play ✓` |
| `GuestSpawner` | `GuestSpawner` | (опц.) `CounterPoint`/`ExitPoint` |
| `Checkholder` | `Checkholder` | `Auto Claim On Arrival ✓` |
| `ServingStation` | `ServingStation` | — |
| `Ledger` | `CafeLedger` | — |

Гости: `Create → TimelessBrew → Guest`. Play → `ServingStation` ПКМ → **Serve PERFECT/WRONG**.

---

## Часть B — Готовка (нужен Input System)

### База
1. **Main Camera** — сверху-сбоку.  2. **`GameInput`**: Point ← Point, Interact ← Interact.
3. **`CursorHandler`**: Game Camera ← Main Camera; Item Layer ← Item; Table Layer ← Table.
4. **`Table`** — плоский Cube, слой Table.  5. **`BrewSession`**.  6. **`DebugHUD`** → `CookingDebugHUD`.
7. **`Skillcheck`** → `Skillcheck` (полоска засыпки).

### Предметы (слой **Item**; обязательно заполнить States + Initial State)

| Объект | Компонент | Item Type | States (Initial) | Доп. поля |
|---|---|---|---|---|
| `Stove` | `Stove` | — | StoveCold/Smolder/Even/Strong | State Cold/Smolder/Even/Strong; Burner Point (перед печкой, на столе), Radius ~0.5; Bellows Handle |
| `BellowsHandle` (доч.) | `InteractableItem` | `BellowsHandle` | Handle (Handle) | Pickable ✗ |
| `Pan` | `RoastingPan` | `Pan` | PanEmpty/Beans/Hot (PanEmpty) | State Empty/Beans/Hot; Stove; Bean Renderer |
| `BeanJar` | `InteractableItem` | `BeanJar` | JarFull (JarFull) | — |
| `Sink` | `Sink` | — | SinkState (SinkState) | State ← SinkState; Fill Point (где стоит турка под краном), Fill Range ~0.5 |
| `Colander` | `Colander` | — | ColanderEmpty/Beans/Clean (ColanderEmpty) | State Empty/Beans/Clean |
| `Grinder` | `Grinder` | — | GrinderEmpty/Beans/Ground (GrinderEmpty) | State Empty/Beans/Ground; Crank Handle |
| `GrinderCrank` (доч.) | `InteractableItem` | `GrinderCrank` | Handle (Handle) | Pickable ✗ |
| `LongSpoon` | `LongSpoon` | `Spoon` | SpoonEmpty/SpoonGround (SpoonEmpty) | State Empty/Ground |
| `Cezve` | `Cezve` | — | CezveEmpty/Coffee/Water/Ready/Ruined (CezveEmpty) | State *; Stove |
| `Kettle` | `Kettle` | — | KettleCold/Hot (KettleCold) | State Cold/Hot; Stove |
| `Cup` | `Cup` | `Cup` | CupEmpty/CupCoffee/CupMilk (CupEmpty) | State Empty/Coffee/Milk |
| `Pitcher` | `Pitcher` | `Pitcher` | PitcherEmpty/Full (PitcherEmpty) | State Empty/Full |
| `MilkJar` | `InteractableItem` | `Milk` | MilkFull (MilkFull) | — |
| `Sugarbowl` | `IngredientAdder` | (любой) | AdditionItem | Kind = Sugar |
| `SyrupBottle` | `IngredientAdder` | (любой) | AdditionItem | Kind = Syrup; Syrup = тип |
| `SpiceJar` | `IngredientAdder` | (любой) | AdditionItem | Kind = Spice; Spice Id = напр. `cocoa` |

### Модель управления (§4.1)
- **Клик** пустой рукой — взять; клик в свободное место — поставить.
- **Удержание ЛКМ** предметом-на-курсоре над целью — лить/высыпать/тереть.
- **Тап/удержание пустой рукой** по станции — мехи (печка), ручка (кофемолка), **кран (раковина)**.
- **Клик добавкой** над целью — одна порция (сахар/сироп/специя).

---

## Часть C — Полное прохождение (по HUD)

1. **Засыпка + скилчек.** Разогрей печь тапами по мехам. `BeanJar` → удержание над сковородой → внизу
   полоска скилчека; **отпусти ЛКМ в зелёной зоне** (точность количества).
2. **Обжарка.** Сковороду к `Burner Point`; сними у `RichBrown`.
3. **Просеивание.** Сковороду → удержание над дуршлагом (зёрна пересыпались, обжарка записана).
   Возьми **дуршлаг** → поднеси к раковине → **держи ЛКМ над раковиной** — чистота растёт; по полной очистке
   просеивание пишется в сессию.
4. **Помол.** `BeanJar` → удержание над кофемолкой. Пустой рукой держи `Crank Handle` — выбери фракцию.
5. **Перенос ложкой.** `LongSpoon` (пустую) → наведи на кофемолку → **клик** (зачерпнул молотый).
   Ложкой удержание над туркой — кофе в турке + помол записан.
6. **Варка.** Вода: **холодная** — поставь турку под кран (рядом с `Fill Point`) и **кликни по раковине** пустой рукой;
   **горячая** — вскипяти `Kettle` на печке и налей в турку. Поставь турку к `Burner Point` — растёт пенка;
   **сними с печки** у верхней черты (взял на курсор) — варка фиксируется, турка готова. Перелив = брак.
   Пустой ложкой над туркой можно усадить пенку.
7. **Разлив (Этап 4).** Возьми пустую `Cup`, поставь; наведи готовую турку на чашку, **держи ЛКМ** — кофе
   наполняет чашку.
8. **Добавки** (по заказу): сахар — клик над туркой/чашкой; сироп — клик над чашкой; специя — клик над туркой.
9. **Латте-арт (Этап 5).** `MilkJar` → удержание над `Pitcher`. `Pitcher` → удержание над `Cup` + **води курсором**.
10. **Подача.** Гость у стойки → `ServingStation` ПКМ → **Serve from BrewSession**.

HUD-строки показывают всё состояние и накопленную **Сессию** (обжарка/шелуха/помол/вода/варка/молоко/латте/сахар/сироп/специи).

---

## Частые промахи
- **Не берётся/не льётся** → не на слое Item, или в CursorHandler не выбраны Item/Table Layer.
- **Источник не льёт** → нет `Can Pour`; цель не принимает → нет `Can Receive`.
- **Тап молет/трясёт/качает/льёт воду, а не берёт** → у станции (кофемолка/мехи/ручка/раковина) состояние `Pickable ✗`.
- **Дуршлаг не трясётся** → у `ColanderBeans` нет `Can Pour`, у `SinkState` нет `Can Receive`, или держишь не над раковиной.
- **Кран не наливает** → турка не в радиусе `Fill Point`, или у неё нет кофе / уже есть вода. Клик по раковине — пустой рукой.
- **Ложка не зачерпывает** → в кофемолке нет молотого, или ложка уже полная.
- **Турка не берёт горячую воду** → сначала кофе (ложкой), потом чайник; чайник должен закипеть (KettleHot).
- **Обжарка/варка/кипение стоят** → печь холодная / перегрета (~10 сек) / предмет не в радиусе Burner Point / ещё «на курсоре».
