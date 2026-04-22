let canvas, ctx, img, gridSize, tileSize;
let tiles = []; // Array to track tile positions
const emptySlot = { x: 0, y: 0 }; // Position of the "hole"

function closePuzzle() {
  document.getElementById('puzzle-overlay').classList.remove('active');
  document.body.classList.remove('puzzle-open');
  document.documentElement.classList.remove('puzzle-open');
  
  // Hide success banner for next time
  const banner = document.getElementById('puzzle-success-banner');
  if (banner) {
    banner.style.display = 'none';
    banner.classList.remove('visible');
  }

  // Clear the canvas to save memory
  if (ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
}

function startPuzzle(imageSrc, size, savedTiles = null, savedEmptySlot = null) {
  gridSize = size;
  img = new Image();
  img.src = imageSrc;
    
  img.onload = function() {
    setupCanvas();
    if (savedTiles) {
      tiles = savedTiles;
      emptySlot.x = savedEmptySlot.x;
      emptySlot.y = savedEmptySlot.y;
    } else {
      initTiles();
      shuffleTiles();
    }
    drawPuzzle();
    document.getElementById('puzzle-overlay').classList.add('active');
    document.body.classList.add('puzzle-open');
    document.documentElement.classList.add('puzzle-open');
  };
}

function setupCanvas() {
  canvas = document.getElementById('puzzle-canvas');
  ctx = canvas.getContext('2d');
    
  // Maintain a playable size (e.g., 400px or 80% of screen)
  const displaySize = Math.min(window.innerWidth * 0.8, 500);
  canvas.width = displaySize;
  canvas.height = displaySize;
  tileSize = canvas.width / gridSize;

  // Add event listener once (check if already added if needed, but here startPuzzle is called once per overlay show)
  // To be safe, we can remove listener before adding or just ensure canvas is global and only listener is added once.
  if (!canvas.onclickSet) {
    canvas.addEventListener('click', handleCanvasClick);
    canvas.onclickSet = true;
  }
}

function initTiles() {
  tiles = [];
  for (let i = 0; i < gridSize * gridSize; i++) {
    tiles.push(i);
  }
  // The last tile (bottom-right) is the empty one
  emptySlot.x = gridSize - 1;
  emptySlot.y = gridSize - 1;
}

function shuffleTiles() {
  // Fisher-Yates shuffle
  for (let i = tiles.length - 2; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [tiles[i], tiles[j]] = [tiles[j], tiles[i]];
  }
    
  // Check for solvability
  if (!isSolvable(tiles, gridSize)) {
    // Swap the first two non-empty tiles to flip parity
    [tiles[0], tiles[1]] = [tiles[1], tiles[0]];
  }
}

function isSolvable(arr, size) {
  let inversions = 0;
  for (let i = 0; i < arr.length - 1; i++) {
    for (let j = i + 1; j < arr.length; j++) {
      if (arr[i] > arr[j] && arr[i] !== size * size - 1 && arr[j] !== size * size - 1) {
        inversions++;
      }
    }
  }
  
  if (size % 2 !== 0) {
    // Odd grid (3x3): solvable if inversions is even
    return inversions % 2 === 0;
  } else {
    // Even grid (4x4): bit more complex based on empty row, 
    // but here empty is always bottom-right (last row).
    // Row counting from bottom (1-indexed), so last row is 1.
    // solvability: (inversions + row_from_bottom) % 2 === 0 (for 0-indexed empty slot)
    // Actually, simpler: if empty is on even row from bottom, inversions must be odd.
    // If empty is on odd row from bottom, inversions must be even.
    // Since our empty is always at bottom-right (row 1, odd), inversions must be even.
    return inversions % 2 === 0;
  }
}

function drawPuzzle() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  for (let i = 0; i < tiles.length; i++) {
    const tileVal = tiles[i];
    if (tileVal === (gridSize * gridSize) - 1) continue; // Skip empty slot

    const destX = (i % gridSize) * tileSize;
    const destY = Math.floor(i / gridSize) * tileSize;

    const srcX = (tileVal % gridSize) * (img.width / gridSize);
    const srcY = Math.floor(tileVal / gridSize) * (img.height / gridSize);

    ctx.drawImage(img, srcX, srcY, img.width / gridSize, img.height / gridSize, 
      destX, destY, tileSize, tileSize);
        
    // Draw tile borders
    ctx.strokeStyle = "#fff";
    ctx.strokeRect(destX, destY, tileSize, tileSize);
  }
}

function handleCanvasClick(e) {
  const rect = canvas.getBoundingClientRect();
  const x = Math.floor((e.clientX - rect.left) / tileSize);
  const y = Math.floor((e.clientY - rect.top) / tileSize);
    
  const clickedIndex = y * gridSize + x;
  const emptyIndex = emptySlot.y * gridSize + emptySlot.x;

  // Check if clicked tile is adjacent to empty slot
  if (isAdjacent(x, y, emptySlot.x, emptySlot.y)) {
    // Swap in array
    [tiles[clickedIndex], tiles[emptyIndex]] = [tiles[emptyIndex], tiles[clickedIndex]];
    // Update empty slot reference
    emptySlot.x = x;
    emptySlot.y = y;
    drawPuzzle();
    checkWin();
    if (typeof currentProductId !== 'undefined') savePuzzleState(currentProductId);
  }
}

  function isAdjacent(x1, y1, x2, y2) {
    return Math.abs(x1 - x2) + Math.abs(y1 - y2) === 1;
  }

  function checkWin() {
    let isWin = true;
    for (let i = 0; i < tiles.length; i++) {
      // In a solved state, tiles[0] = 0, tiles[1] = 1, etc.
      if (tiles[i] !== i) {
        isWin = false;
        break;
      }
    }

    if (isWin) {
      // Draw the full image one last time to fill the empty slot
      ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        
      // Trigger the reward
      handleVictory();
    }
  }

function handleVictory() {
  const message = document.getElementById('puzzle-message');
  message.innerHTML = "<h3>🎉 You solved it!</h3><p>Use code <b>PUZZLE5</b> for 5% off!</p>";
  
  // Clear the saved state once they win
  if (typeof currentProductId !== 'undefined') {
    localStorage.removeItem(`puzzle_state_${currentProductId}`);
  }
  
  const badge = document.querySelector('.puzzle-hint-badge');
  if (badge) badge.remove();

  // Optional: Fire an AJAX call to the server to log the win or 
  fetch(`/Plugins/ImagePuzzle/MarkAsSolved?productId=${currentProductId}`, { method: 'POST' })
    .then(response => response.json())
    .then(data => {
      if (data.success) {
        const banner = document.getElementById('puzzle-success-banner');
        if (banner) {
          banner.innerHTML = "✨ <b>Discount applied for this product!</b> Your savings are ready at checkout.";
          banner.style.display = 'block';
          banner.classList.add('visible');
        }
      }
    });
  });
}
  function savePuzzleState(productId) {
    const state = {
      tiles: tiles,
      emptySlot: emptySlot,
      gridSize: gridSize,
      imageSrc: img.src
    };
    localStorage.setItem(`puzzle_state_${productId}`, JSON.stringify(state));
  }

  function loadPuzzleState(productId) {
    const saved = localStorage.getItem(`puzzle_state_${productId}`);
    if (saved) {
      return JSON.parse(saved);
    }
    return null;
  }


