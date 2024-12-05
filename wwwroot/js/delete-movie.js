(() => {
    function configurePage() {
        const deleteMovieButton = document.getElementById('delete-movie');
        
        if (deleteMovieButton) {
            const movieId = deleteMovieButton.dataset.movieid;
            deleteMovieButton.addEventListener('click', async (evt) => {
                const ans = confirm(`Delete movie with id ${movieId}?`);
                if (ans !== true) return;
                
                const deleteRes = await fetch(`/api/v2/movie/${movieId}`, { method: 'DELETE' });
                if (!deleteRes.ok) {
                    alert ("Error; see console");
                    console.log(deleteRes);
                }
                else {
                    location.pathname = '/movies';
                }
            });
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        configurePage();
    });
})();