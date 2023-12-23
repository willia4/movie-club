/** @typedef UpdateRatingResult
 *  @type {object}
 *  @property {number} newRating
 *  @property {string} newRatingFormatted
 *  @property {number} averageMovieRating
 *  @property {string} averageMovieRatingFormatted
 */
(() => {
    /**@function
     * 
     * @param {HTMLButtonElement} button
     */
    function configureButton(button) {
        const userId = button.dataset.userid;
        const movieId = button.dataset.movieid;
        /** @type {HTMLDivElement} */
        const validation = document.getElementById("validation-" + userId);
        /** @type {HTMLInputElement} */
        const input = document.getElementById("update-rating-input-" + userId);
        
        const showValidation = (msg) => {
            validation.innerText = msg;
            validation.classList.remove("not-shown");
        }

        const hideValidation = (msg) => {
            validation.innerText = '';
            validation.classList.add("not-shown");
        }

        button.addEventListener("click", async (evt) => {
            const value = (input.value || '').trim();
            hideValidation();
            if (value.length === 0) {
                showValidation("Required");
                return;
            }
            
            if (!/\d*\.?\d*/.test(value)) {
                showValidation("Only a decimal number is allowed");
                return;
            }
            
            const decimalLocation = value.indexOf('.');

            if (decimalLocation >= 0) {
                const parts = value.split('.');
                if (parts.length !== 2) {
                    showValidation("Only a single decimal is allowed");
                    return;
                }
                const decimalPart = parts[1];
                if (parts[1].length > 2) {
                    showValidation("Only two-digits of precision are allowed");
                    return;
                }
            }
            
            const parsed = parseFloat(value);
            if (isNaN(parsed)) {
                showValidation("Could not parse value");
                return;
            }
            
            if (parsed < 0 || parsed > 10) {
                showValidation("Only values between 0.00 and 10.00 are allowed");
                return;
            }
            
            input.disabled = true;
            button.disabled = true;
            button.classList.remove('not-loading');
            button.classList.add('loading');
            
            const res = await fetch(`/api/v1/movies/${movieId}/users/${userId}`, {
               method: "POST",
               body: JSON.stringify({
                   newRating: parsed
               }),
                headers: {
                   "Content-Type": "application/json"
                }
            });
            
            if (!res.ok) {
                console.log(res.status);
                showValidation("Error from API");
            }
            else
            {
                hideValidation();

                /** @type {UpdateRatingResult} */
                const body = JSON.parse(await res.text());
                input.value = body.newRatingFormatted;
                
                const ourRatingDisplay = document.getElementById("our-rating");
                if (ourRatingDisplay) {
                    ourRatingDisplay.innerText = body.averageMovieRatingFormatted;
                }
            }

            input.disabled = false;
            button.disabled = false;
            button.classList.add('not-loading');
            button.classList.remove('loading');
        })
    }
    
    function configurePage() {
        /**@type {HTMLButtonElement[]} */
        const buttons= Array.from(document.querySelectorAll("button.update-rating-button"));
        for (const b of buttons) {
            configureButton(b);
        }
    }
    
    document.addEventListener("DOMContentLoaded", () => {
        configurePage();
    });
})();