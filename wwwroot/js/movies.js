/**@typedef ApiSearchResult
 * @type {object}
 * @property {string} id
 * @property {string} title
 * @property {string} overview
 * @property {string} posterHref
 * @property {string} releaseDate
 */

(() => {
    const searchForm = document.getElementById("search-form");
    const searchBox = document.querySelector("div.search-box");
    const searchBoxInput = document.getElementById("search-box");
    const searchResults = document.querySelector("div.search-results");
    const searchButton = document.getElementById("search-button");
    
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
        // showSearchResultsWithSpinner();
        // setSearchResultsDisplay(fakeResults);
        /** END DEBUG **/
    });

    function clearSearchResultsDisplay() {
        if (searchResults) {
            let firstChild = searchResults.firstChild;
            while (firstChild) {
                searchResults.removeChild(firstChild);
                firstChild = searchResults.firstChild;
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