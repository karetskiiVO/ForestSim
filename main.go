package main

import (
	"image/color"
	"log"
	"math/rand"
	"time"

	"github.com/hajimehoshi/ebiten/v2"
	"github.com/hajimehoshi/ebiten/v2/inpututil"
)

const (
	screenWidth  = 640
	screenHeight = 480
	pointCount   = 100
	pointSize    = 5
)

// Point представляет точку на плоскости
type Point struct {
	X float64
	Y float64
}

// Game реализует интерфейс ebiten.Game
type Game struct {
	points []Point
	rng    *rand.Rand
}

// NewGame создает новую игру с начальным набором точек
func NewGame() *Game {
	g := &Game{
		points: make([]Point, pointCount),
		rng:    rand.New(rand.NewSource(time.Now().UnixNano())),
	}
	g.generatePoints()
	return g
}

// generatePoints создает новый набор случайных точек
func (g *Game) generatePoints() {
	for i := range g.points {
		g.points[i] = Point{
			X: g.rng.Float64() * screenWidth,
			Y: g.rng.Float64() * screenHeight,
		}
	}
}

// Update обновляет состояние игры
func (g *Game) Update() error {
	// Обновляем точки при нажатии пробела
	if inpututil.IsKeyJustPressed(ebiten.KeySpace) {
		g.generatePoints()
	}
	return nil
}

// Draw отрисовывает игровой экран
func (g *Game) Draw(screen *ebiten.Image) {
	// Очищаем экран (заливаем черным цветом)
	screen.Fill(color.RGBA{0, 0, 0, 255})

	// Отрисовываем точки
	for _, p := range g.points {
		// Рисуем каждую точку как маленький квадрат
		for offsetY := 0; offsetY < pointSize; offsetY++ {
			for offsetX := 0; offsetX < pointSize; offsetX++ {
				screen.Set(int(p.X)+offsetX, int(p.Y)+offsetY, color.RGBA{255, 255, 255, 255})
			}
		}
	}
}

// Layout возвращает размеры игрового экрана
func (g *Game) Layout(outsideWidth, outsideHeight int) (int, int) {
	return screenWidth, screenHeight
}

func main() {
	ebiten.SetWindowSize(screenWidth, screenHeight)
	ebiten.SetWindowTitle("Случайные точки (нажмите пробел для обновления)")

	if err := ebiten.RunGame(NewGame()); err != nil {
		log.Fatal(err)
	}
}
