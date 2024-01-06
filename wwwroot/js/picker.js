(() => {
    document.addEventListener("DOMContentLoaded", () => {
        const shuffleButton = document.getElementById("shuffle-button");
        const resetShuffleButton = document.getElementById("reset-shuffle-button");
        
        if (shuffleButton) {
            shuffleButton.addEventListener("click", async (evt) => {
                evt.preventDefault();
                evt.stopPropagation();

                const res = await fetch(`/api/v1/picker/shuffle`, {
                    method: "POST"
                });

                if (res.ok) {
                    window.location.reload();
                } else {
                    console.log(`Could not shuffle: ${res.status}`);
                }
            });
        }

        if (resetShuffleButton) {
            resetShuffleButton.addEventListener("click", async (evt) => {
                evt.preventDefault();
                evt.stopPropagation();

                const res = await fetch(`/api/v1/picker/shuffle`, {
                    method: "DELETE"
                });

                if (res.ok) {
                    window.location.reload();
                } else {
                    console.log(`Could not reset shuffle: ${res.status}`);
                }
            });
        }
    });
})();