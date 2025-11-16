# Lanius Frontend - Minimalist Sci-Fi Visualization

## Overview

Clean, technical interface for Git repository visualization with a futuristic aesthetic. Monochrome color scheme with thin lines, smooth animations, and precise geometry.

## Design Philosophy

### Visual Style
- **Monochrome**: Black/dark gray on white/light gray background
- **Precision**: Thin lines (1px), geometric shapes
- **Technical**: HUD-like interface, schematic appearance
- **Smooth**: Easing curves, fade transitions
- **Minimalist**: No clutter, essential information only

### Typography
- **Headers**: Monospace, uppercase, letter-spacing
- **Data**: Monospace for technical readability
- **Labels**: Small, gray, non-intrusive

## File Structure

```
src/Lanius.Web/
??? index.html           # Main application page
??? css/
?   ??? styles.css       # Minimalist sci-fi styling
??? js/
    ??? app.js           # Application logic & API calls
    ??? visualization.js # D3.js rendering engine
```

## Features

### Repository Management
- Clone repositories via HTTPS URL
- Display repository statistics
- Real-time monitoring with 5-second polling

### Branch Visualization
- Branch lines as horizontal lanes
- Pattern-based filtering (e.g., `main`, `release/*`)
- Branch labels on the left

### Commit Rendering
- Commits as small circles on branch lines
- Size based on total changes (logarithmic scale)
- Color intensity based on additions/deletions ratio
- Smooth fade-in animations
- Hover tooltips with commit details
- Click to show full commit details

### Replay Mode
- Animated commit history playback
- Adjustable speed (0.1x to 5x)
- Pause/resume controls
- Progress tracking
- Real-time stats updates

### Real-Time Updates
- SignalR connection for live updates
- New commits appear with pulse animation
- Repository stats update automatically

## Color Coding

### Commit Colors (Monochrome Gradient)
- **Darker shades**: More additions than deletions
- **Lighter shades**: More deletions than additions
- **Mid-gray**: Balanced changes

### Size Coding
- **Small (4px)**: Few changes
- **Medium (5-6px)**: Moderate changes
- **Large (7px)**: Many changes

## Animations

### Fade-In (New Elements)
```
Duration: 500ms
Easing: ease-out
Scale: 0.8 ? 1.0
Opacity: 0 ? 1
```

### Pulse (New Commits)
```
Duration: 1500ms total
Step 1: Grow to 1.5x, fade to 0.4 (1000ms)
Step 2: Shrink to 1x, fade to 1.0 (500ms)
```

### Hover (Interaction)
```
Duration: 200ms
Easing: cubic-out
Radius: +2px
Stroke: 1px ? 2px
```

## Usage

### Starting the Application

1. **Start the API**:
```bash
cd src/Lanius.Api
dotnet run
```

2. **Open Browser**:
Navigate to `https://localhost:5001`

### Cloning a Repository

1. Enter repository URL in the sidebar
2. Click "Clone"
3. Wait for visualization to appear

### Filtering Branches

1. Enter patterns in "Branch Filter" (e.g., `main, release/*`)
2. Click "Apply"
3. Visualization updates to show only matching branches

### Using Replay Mode

1. Ensure repository is cloned
2. Adjust speed slider (0.1x - 5x)
3. Click "Start" to begin replay
4. Use Pause/Resume/Stop controls
5. Watch commits appear in chronological order

### Monitoring Real-Time

1. Clone a repository
2. Click "Start" in Real-Time Monitor section
3. Make changes to the repository and push
4. New commits appear automatically within 5 seconds

## Keyboard Shortcuts

*Future enhancement - not yet implemented*

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+

## Dependencies

### External Libraries
- **D3.js v7** - Data visualization
- **SignalR 8.0** - Real-time communication

### Loaded from CDN
```html
<script src="https://d3js.org/d3.v7.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.0/dist/browser/signalr.min.js"></script>
```

## Customization

### Adjusting Colors

Edit `css/styles.css`:
```css
:root {
    --bg-primary: #fafafa;      /* Main background */
    --fg-primary: #000000;      /* Text and lines */
    --line-color: #1a1a1a;      /* Commit strokes */
    --line-color-light: #4a4a4a; /* Links */
}
```

### Adjusting Layout

Edit `css/styles.css`:
```css
.sidebar {
    width: 300px;  /* Sidebar width */
}

:root {
    --spacing-md: 16px;  /* Standard spacing */
}
```

### Adjusting Visualization

Edit `js/visualization.js`:
```javascript
const config = {
    commitRadius: 4,           // Base commit size
    branchSpacing: 40,         // Vertical space between branches
    lineWidth: 1,              // Stroke width
}
```

## Performance

### Optimization Strategies
- Efficient D3 data binding
- Debounced window resize
- Minimal DOM manipulation
- CSS hardware acceleration
- Lazy rendering for large datasets

### Tested Scenarios
- 1000+ commits: Smooth
- 50+ branches: Readable
- Real-time updates: < 100ms
- Replay at 5x: Fluid

## Troubleshooting

### CORS Errors
Ensure API `Program.cs` includes frontend origin:
```csharp
policy.WithOrigins("https://localhost:5001")
```

### SignalR Connection Failed
1. Check API is running
2. Verify hub URL in `js/app.js`
3. Check browser console for errors

### Visualization Not Rendering
1. Check browser console for D3 errors
2. Verify commits and branches data loaded
3. Inspect SVG element in DevTools

### Commits Not Appearing
1. Verify repository was cloned successfully
2. Check API response in Network tab
3. Ensure branch filter isn't excluding all commits

## Future Enhancements

### Planned Features
- [ ] Zoom and pan controls
- [ ] Timeline scrubber for replay
- [ ] Commit search and filtering
- [ ] Export visualization as SVG/PNG
- [ ] Dark mode toggle
- [ ] Keyboard shortcuts
- [ ] Multiple repository comparison
- [ ] Advanced branch metrics

### Design Improvements
- [ ] Curved branch merge lines
- [ ] Animated branch splits
- [ ] Mini-map overview
- [ ] Commit density heatmap
- [ ] Author avatars

## Credits

- **D3.js**: Data-Driven Documents
- **SignalR**: Real-time web communication
- **Design**: Minimalist sci-fi aesthetic inspired by technical schematics and HUD interfaces
