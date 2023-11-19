/**@typedef ApiSearchResult
 * @type {object}
 * @property {string} id
 * @property {string} title
 * @property {string} overview
 * @property {string} posterHref
 * @property {string} releaseDate
 */

/**@typedef ApiDetailResult
 * @type {object}
 * @property {string} id
 * @property {string} title
 * @property {string} overview
 * @property {string} posterHref
 * @property {string} backdropHref
 * @property {string} releaseDate
 * @property {number} runtimeMinutes
 */
(() => {
    const searchForm = document.getElementById("search-form");
    const searchBox = document.querySelector("div.search-box");
    const searchBoxInput = document.getElementById("search-box");
    const searchResults = document.querySelector("div.search-results");
    const searchButton = document.getElementById("search-button");
    const cancelButton = document.getElementById("search-cancel");

    const searchResultTemplate = document.getElementById("search-result-template");
    const spinnerTemplate = document.getElementById("search-result-spinner-template");
    /** @function
     *
     * @param {string} title
     * @returns {Promise<ApiSearchResult[]>}
     */
    async function sendSearchRequest(title) {
        const res = await fetch("/api/v1/search", {
            method: "POST",
            body: JSON.stringify({
                titleSearch: title
            }),
            headers: {
                "Content-Type": "application/json"
            }
        });

        if (!res.ok) {
            throw new Error("Invalid response from server: " + res.status);
        }

        const body = await res.text();
        return JSON.parse(body);
    }

    /** @function
     * 
     * @param {string} id
     * @returns {Promise<ApiDetailResult>}
     */
    async function sendDetailsRequest(id){
        const res = await fetch("/api/v1/movie/" + id, { method: "GET" });
        if (!res.ok) {
            throw new Error("Invalid response from server: " + res.status);
        }
        
        const body = await res.text();
        return JSON.parse(body);
    }

    document.addEventListener("DOMContentLoaded", () => {
        window.addEventListener("resize", (evt) => {
            setSearchResultsDisplayWidth();
        });
        setSearchResultsDisplayWidth();

        if (searchButton && searchBoxInput) {
            searchBoxInput.addEventListener("keyup", () => {
               searchButton.disabled = !!!searchBoxInput.value; 
            });
            searchButton.disabled = true;
        }

        if (searchButton && searchBoxInput && searchForm) {
            async function doSearch(evt ) {
                evt.preventDefault();

                if (searchBoxInput.value) {
                    showSearchResultsWithSpinner();
                    setSearchResultsDisplay(await sendSearchRequest(searchBoxInput.value));
                }
            }
            
            searchButton.addEventListener("click", doSearch);
            searchForm.addEventListener("submit", doSearch);
        }

        if (cancelButton) {
            cancelButton.addEventListener("click", (evt) => {
                evt.preventDefault();
                searchResults.classList.add("hidden");
            })
        }
        
        /** DEBUG **/
        const fakeResults = [
            {
                "id": "256961",
                "title": "Paul Blart: Mall Cop 2",
                "overview": "Security guard Paul Blart is headed to Las Vegas to attend a Security Guard Expo with his teenage daughter Maya before she departs for college. While at the convention, he inadvertently discovers a heist - and it's up to Blart to apprehend the criminals.",
                "posterHref": "https://image.tmdb.org/t/p/w185//zgr98ZRQnmN8iWzJn1EelAGFaTs.jpg",
                "releaseDate": "2015-04-16"
            },
            {
                "id": "14560",
                "title": "Paul Blart: Mall Cop",
                "overview": "Mild-mannered Paul Blart has always had huge dreams of becoming a State Trooper. Until then, he patrols the local mall as a security guard. With his closely cropped moustache, personal transporter and gung-ho attitude, only Blart seems to take his job seriously. All that changes when a team of thugs raids the mall and takes hostages. Untrained, unarmed and a super-size target, Blart has to become a real cop to save the day.",
                "posterHref": "https://image.tmdb.org/t/p/w185//A4zZv0Q1VKURFZFEl2vwjaE2q0g.jpg",
                "releaseDate": "2009-01-15"
            }
        ];
        showSearchResultsWithSpinner();
        setSearchResultsDisplay(fakeResults);
        /** END DEBUG **/
    });

    function clearSearchResultsDisplay() {
        function findNextNodeToDelete() {
            for(let child of searchResults.children) {
                if (!child.classList.contains("do-not-clear")) {
                    return child;
                }
            }
            
            return undefined;
        }

        if (searchResults) {
            let next = findNextNodeToDelete();
            while (next) {
                if (next)
                searchResults.removeChild(next);
                next = findNextNodeToDelete();
            }
        }
    }

    /** @function
     * @param {ApiSearchResult[]} results
     */
    function setSearchResultsDisplay(results) {
        clearSearchResultsDisplay();
        results.forEach(appendSearchResult);
    }

    /** @function
     * @param {ApiSearchResult} result
     */
    function appendSearchResult(result) {
        if (!searchResultTemplate) {
            console.error("Could not load search result template");
            return;
        }
        
        const movieId = result.id;
        const newEl = searchResultTemplate.content.cloneNode(true);
        newEl.querySelector("a.title-link").href = "https://www.themoviedb.org/movie/" + result.id;
        newEl.querySelector("div.title").textContent = result.title;
        newEl.querySelector("div.release-date").textContent = result.releaseDate;
        newEl.querySelector("div.overview").textContent = result.overview;
        
        if (result.posterHref) {
            newEl.querySelector("div.search-result-image-container img").src = result.posterHref; 
        } else {
            newEl.querySelector("div.search-result-image-container img").setAttribute("style", "display: none");
        }

        newEl.querySelector(".search-result-add-button-container button").addEventListener("click", async (evt) => {
            evt.preventDefault();
            
            // first, disable all existing buttons; no need to worry about re-enabling them later, because they'll be destroyed the next 
            // time this view is shown
            const buttons = Array.from(document.querySelectorAll(".search-result-container .search-result-add-button-container button"));
            for (let b of buttons) {
                b.disabled = true;
            }
            
            // second, just for our button, swap it to the loading spinner
            evt.currentTarget.classList.add("loading");
            evt.currentTarget.classList.remove("not-loading");
            
            try {
                const details= await sendDetailsRequest(movieId);
                document.getElementById("title").value = details.title;
                document.getElementById("overview").value = details.overview;
                document.getElementById("runtime").value = details.runtimeMinutes;
                document.getElementById("tmdb-id").value = details.id;
                document.getElementById("tmdb-poster").value = details.posterHref;
            } 
            catch (err) {
                console.log(err);
                alert("Could not copy movie details: " + err);
            }

            searchResults.classList.add("hidden");
        })
        searchResults.appendChild(newEl);
    }

    function setSearchResultsDisplayWidth() {
        if (searchBox && searchResults) {
            const bounds = searchBox.getBoundingClientRect();
            searchResults.setAttribute("style", "width:" + bounds.width + "px");
        }
    }
    
    function searchResultsAreDisplayed() {
        if (searchResults) {
            return searchResults.classList.contains("hidden");
        }
    }
    
    function showSearchResultsWithSpinner() {
        function addSpinnerElement() {
            const newEl = spinnerTemplate.content.cloneNode(true);
            searchResults.appendChild(newEl);
        }

        if (searchResults) {
            clearSearchResultsDisplay();
            addSpinnerElement();
            searchResults.classList.remove("hidden");
        }
    }
})();