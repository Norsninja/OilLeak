# OilLeak - Technical Documentation

## System Architecture

### Core Game Loop
```
GameController (Singleton)
    ├── GameState (NotStarted → Active → Over)
    ├── RoundTimer (Countdown)
    ├── OilController (Particle emission)
    ├── ScoringManager (Track performance)
    └── UIController (Display state)
```

### Data Flow
1. **Input** → PlayerController → InventoryController
2. **Physics** → ItemController → ParticleCollision
3. **Collision** → OilController → ScoringManager
4. **Score** → UIController → Display

## Key Systems

### Oil Leak System

**OilController.cs**
- Manages ParticleSystem emission
- Tracks blocked vs escaped particles
- Escalates emission rate over time
- Triggers audio/visual feedback

**Configuration (OilLeakData.cs)**
```csharp
[CreateAssetMenu]
public class OilLeakData : ScriptableObject {
    public float EmissionRate;
    public float EscalationRate;
    public int MaxParticles;
}
```

### Inventory System

**InventoryController.cs**
- Manages available items
- Handles item instantiation
- Tracks weight limits
- Processes drop commands

**Item Properties**
```csharp
public class Item : MonoBehaviour {
    public float Weight;
    public float BlockingRadius;
    public bool IsRagdoll;
    public AudioClip ImpactSound;
}
```

### Scoring System

**Score Calculation**
```
Base Score = Particles Blocked × 10
Efficiency = (Blocked / Total) × 100
Throw Efficiency = (Successful Throws / Total) × 100
Final Grade = CalculateLetterGrade(Base + Bonuses)
```

**Grade Thresholds**
- S: > 95% efficiency + bonus
- A: > 90% efficiency
- B: > 75% efficiency
- C: > 60% efficiency
- D: > 40% efficiency
- F: < 40% efficiency

### Physics Configuration

**Collision Layers**
- Layer 6: Water Surface
- Layer 7: Oil Particles
- Layer 8: Dropped Items
- Layer 9: Ocean Floor

**Particle Collision Detection**
```csharp
void OnParticleCollision(GameObject other) {
    if (other.CompareTag("DroppedItem")) {
        // Block particle
        IncrementBlockedCount();
    }
}
```

## ScriptableObject Assets

### Required Assets
Create these in Unity:
1. **OilLeakData** (Assets/ScriptableObjects/OilLeakData/)
2. **RoundLocationData** (Assets/ScriptableObjects/RoundData/)
3. **InventoryItemData** (Assets/ScriptableObjects/Items/)

### Asset References
Must be wired in Inspector:
- GameController → GameState
- OilController → OilLeakData
- InventoryController → Item prefabs
- RoundManager → RoundLocationData

## Performance Optimization

### Current Issues
1. **FindObjectOfType calls** - Replace with cached references
2. **No object pooling** - Implement for frequently spawned items
3. **Particle count** - Reduce for WebGL (max 500)

### WebGL Specific
```csharp
#if UNITY_WEBGL
    particleSystem.main.maxParticles = 500;
    QualitySettings.shadowDistance = 0;
    QualitySettings.antiAliasing = 0;
#endif
```

### Recommended Optimizations
1. **Texture Compression**: Use DXT5 for desktop, ASTC for mobile
2. **Audio**: Convert WAV to compressed OGG Vorbis
3. **Models**: Reduce poly count, combine meshes
4. **Scripts**: Remove Debug.Log calls in builds

## Build Configuration

### WebGL Settings
```
Player Settings:
- Color Space: Linear
- API Compatibility: .NET Standard 2.1
- Publishing Settings:
  - Compression Format: Brotli
  - Decompression Fallback: true
  - Memory Size: 512MB
  - WebAssembly Streaming: true
```

### Quality Settings
Create three profiles:
1. **Low**: Mobile/weak devices
2. **Medium**: Standard web
3. **High**: Desktop with good GPU

## State Machine Patterns

### Game State
```csharp
public enum GameState {
    MainMenu,
    Playing,
    Paused,
    RoundOver,
    GameOver
}
```

### Round Flow
```
MainMenu → StartRound() → Playing
    ↓                          ↓
    Quit                   Timer Expires
                               ↓
                           RoundOver
                               ↓
                    Show Score → Next Round or GameOver
```

## Event System

### Current Events
- `OnOilBlocked` - Particle successfully blocked
- `OnOilEscaped` - Particle reached surface
- `OnItemDropped` - Player dropped item
- `OnRoundComplete` - Timer expired
- `OnScoreCalculated` - Final score ready

### Adding New Events
```csharp
public static event Action<int> OnEventName;
OnEventName?.Invoke(value);
```

## Debug Commands

Add to GameController for testing:
```csharp
void Update() {
    #if UNITY_EDITOR
    if (Input.GetKeyDown(KeyCode.F1)) InstantWin();
    if (Input.GetKeyDown(KeyCode.F2)) AddScore(1000);
    if (Input.GetKeyDown(KeyCode.F3)) SpawnAllItems();
    #endif
}
```

## Common Issues & Solutions

### Issue: Particles not colliding
**Solution**: Check Particle System collision module, ensure "Send Collision Messages" is enabled

### Issue: Items falling through ocean floor
**Solution**: Verify Rigidbody collision detection set to "Continuous"

### Issue: UI not updating
**Solution**: Check UIController references in Inspector

### Issue: Can't restart game
**Solution**: Implement `GameController.ResetRound()` method

## Testing Checklist

### Pre-Build
- [ ] All ScriptableObjects assigned
- [ ] No missing prefab references
- [ ] Debug.Log statements removed
- [ ] Quality settings configured

### WebGL Build
- [ ] File size < 100MB
- [ ] Loads in < 10 seconds
- [ ] Maintains 30+ FPS
- [ ] Mobile touch controls work

### Gameplay
- [ ] Oil particles emit correctly
- [ ] Items block particles
- [ ] Score calculates properly
- [ ] Game over triggers
- [ ] Can restart game

## Future Improvements

### Phase 1 (MVP)
- Fix restart functionality
- Add main menu
- Implement difficulty progression

### Phase 2 (Polish)
- Object pooling system
- Particle optimization
- Audio manager
- Save system

### Phase 3 (Features)
- Leaderboards
- Multiple rounds
- New item types
- Achievement system