# CapFrameX.OSD - Native GPU Quad Pillar Bars Architecture & Implementation Patch

This document specifies the architecture, SHM contract, and exact C++ implementation for rendering tall, hardware-accelerated CPU load pillars (гистограммы нагрузки ядер) in `CapFrameX.OSD` (D3D11, D3D12, Vulkan, and DWM overlay).

---

## 1. Problem Statement & Motivation

- **Font Glyphs Limitations**: Text glyphs (` `, `▂`, `▃`, `▄`, `▅`, `▆`, `▇`, `█`) are physically constrained to the text font line height (~14-18px). Scaling font size scales both width and height, causing line collisions and awkward spacing.
- **Buffer Limitations**: In-game SHM `szValueText` buffer is 64 bytes. UTF-8 multi-byte glyphs (3 bytes per block) limit the number of threads that can be displayed to 16-20 threads before truncation.
- **Hardware Quads Solution**: By rendering real textured/colored GPU quads directly in the render pipeline (D3D11, D3D12, Vulkan, Direct2D/DirectWrite), pillars can be rendered **3x to 5x taller** than standard text lines (e.g. 45-60px), with smooth load levels (0-100%) and support for up to 63 cores/threads packed into binary bytes in `szValueText`.

---

## 2. Shared Memory Contract

The shared memory map `Global\CfxOsdMetricsV1` maintains its binary record size of **376 bytes**:

```cpp
#pragma pack(push, 1)
struct OsdEntryRecord {
    char id[64];         // Entry identifier e.g. "CpuThreadsLoadBar"
    char group[64];      // Group name e.g. "CPU Threads"
    char label[96];      // Label
    char value_text[64]; // [0] = core_count (1..63), [1..N] = load percent (0..100)
    char unit[16];       // Unit string
    double value;        // Scalar value
    double upper_limit;
    double lower_limit;
    int32_t is_numeric;
    uint32_t color;      // RGBA color of active fill
    int32_t show_graph;  // 2 = GRAPH_TYPE_CPU_PILLARS (or 1 if detected by id)
    int32_t digits;
    int32_t has_upper;
    uint32_t upper_color;
    int32_t has_lower;
    uint32_t lower_color;
    int32_t separators;
    uint32_t group_color;
    int32_t group_scale;
    int32_t value_scale; // Scale factor: 100 = 1.0x, 200 = 2.0x, 300 = 3.0x height
};
#pragma pack(pop)
static_assert(sizeof(OsdEntryRecord) == 376, "Record size must remain 376 bytes");
```

---

## 3. C++ Layout Engine Patch (`overlay_layout.cpp`)

When computing the vertical height and position of rows in the OSD panel, check if the entry represents a graphical CPU pillar:

```cpp
// Returns the allocated vertical space for this row in pixels
float CalculateRowHeight(const OsdEntryRecord& entry, float baseLineHeight, float zoomFactor)
{
    // Check if entry is a CPU pillar graph
    if (entry.show_graph == 2 || 
        (strncmp(entry.id, "Cpu", 3) == 0 && strstr(entry.id, "LoadBar") != nullptr))
    {
        float scale = (entry.value_scale > 0) ? (entry.value_scale / 100.0f) : 2.5f;
        // Allocate 3x - 4x standard line height
        return baseLineHeight * scale * zoomFactor;
    }

    return baseLineHeight * zoomFactor;
}
```

---

## 4. C++ Rendering Implementation (`render_cpu_pillars.cpp`)

Shared geometry generation for D3D11, D3D12, and Vulkan render batches:

```cpp
#include <cstdint>
#include <algorithm>

struct RenderQuadVertex {
    float x, y;
    uint32_t color;
};

// Generates vertices for background track quads and active load fill quads
void BuildCpuPillarQuads(
    const OsdEntryRecord& entry,
    float originX,
    float originY,
    float baseLineHeight,
    float zoom,
    std::vector<RenderQuadVertex>& outVertices)
{
    const uint8_t* payload = reinterpret_cast<const uint8_t*>(entry.value_text);
    uint8_t coreCount = payload[0];
    
    // Safety fallback: if payload is plain text, do not render binary quads
    if (coreCount == 0 || coreCount > 63)
        return;

    float heightMultiplier = (entry.value_scale > 0) ? (entry.value_scale / 100.0f) : 2.5f;
    float totalHeight = baseLineHeight * heightMultiplier * zoom;
    float barWidth = 7.0f * zoom;
    float barGap = 3.0f * zoom;

    // Track background: translucent dark grey (0x33000000 or 0x40202020)
    uint32_t trackBgColor = 0x44202020;
    
    // Fill color from entry (or default CapFrameX green 0xFF35BA35)
    uint32_t activeColor = entry.color != 0 ? entry.color : 0xFF35BA35;

    float currentX = originX;
    float bottomY = originY + totalHeight;

    for (uint8_t i = 0; i < coreCount; ++i)
    {
        uint8_t loadPercent = payload[1 + i];
        if (loadPercent > 100) loadPercent = 100;

        float fillHeight = (loadPercent / 100.0f) * totalHeight;
        if (fillHeight < 2.0f * zoom) fillHeight = 2.0f * zoom; // Baseline visible mark

        // 1. Background quad (full height)
        // Quad 1: [currentX, originY] to [currentX + barWidth, bottomY]
        AddQuad(outVertices, currentX, originY, currentX + barWidth, bottomY, trackBgColor);

        // 2. Active load quad (rises from bottom)
        // Quad 2: [currentX, bottomY - fillHeight] to [currentX + barWidth, bottomY]
        AddQuad(outVertices, currentX, bottomY - fillHeight, currentX + barWidth, bottomY, activeColor);

        currentX += barWidth + barGap;
    }
}

inline void AddQuad(
    std::vector<RenderQuadVertex>& v,
    float left, float top, float right, float bottom, uint32_t color)
{
    // Triangle 1
    v.push_back({ left, top, color });
    v.push_back({ right, top, color });
    v.push_back({ left, bottom, color });
    // Triangle 2
    v.push_back({ right, top, color });
    v.push_back({ right, bottom, color });
    v.push_back({ left, bottom, color });
}
```

---

## 5. Direct2D / DWM Desktop Overlay Implementation

For `cfx_osd_core.dll` (Desktop OSD via Direct2D `ID2D1RenderTarget`):

```cpp
void RenderCpuPillarsD2D(
    ID2D1RenderTarget* rt,
    ID2D1SolidColorBrush* trackBrush,
    ID2D1SolidColorBrush* fillBrush,
    const OsdEntryRecord& entry,
    D2D1_POINT_2F origin,
    float baseLineHeight,
    float zoom)
{
    const uint8_t* payload = reinterpret_cast<const uint8_t*>(entry.value_text);
    uint8_t count = payload[0];
    if (count == 0 || count > 63) return;

    float heightMult = (entry.value_scale > 0) ? (entry.value_scale / 100.0f) : 2.5f;
    float totalHeight = baseLineHeight * heightMult * zoom;
    float barWidth = 7.0f * zoom;
    float barGap = 3.0f * zoom;

    float curX = origin.x;
    float bottomY = origin.y + totalHeight;

    for (uint8_t i = 0; i < count; ++i)
    {
        uint8_t load = payload[1 + i];
        if (load > 100) load = 100;
        float fillHeight = std::max((load / 100.0f) * totalHeight, 2.0f * zoom);

        // Draw track
        D2D1_RECT_F trackRect = D2D1::RectF(curX, origin.y, curX + barWidth, bottomY);
        rt->FillRectangle(trackRect, trackBrush);

        // Draw fill
        D2D1_RECT_F fillRect = D2D1::RectF(curX, bottomY - fillHeight, curX + barWidth, bottomY);
        rt->FillRectangle(fillRect, fillBrush);

        curX += barWidth + barGap;
    }
}
```

---

## 6. C# Data Packing Integration (`OverlayEntryAdapter.cs`)

When transferring entries from CapFrameX to `OsdEntry`:

```csharp
// In OverlayEntryAdapter.cs
if (e.Identifier == "CpuThreadsLoadBar" || e.Identifier == "CpuCoreLoadsBar")
{
    if (e.Value is IReadOnlyList<double> loads && loads.Count > 0)
    {
        byte count = (byte)Math.Min(loads.Count, 63);
        byte[] payload = new byte[count + 1];
        payload[0] = count;
        for (int i = 0; i < count; i++)
        {
            payload[1 + i] = (byte)Math.Clamp((int)Math.Round(loads[i]), 0, 100);
        }
        o.ValueText = Encoding.Latin1.GetString(payload);
        o.ShowGraph = true; // Signals native renderer to invoke pillar quad builder
    }
}
```
