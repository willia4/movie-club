(() => {
    let unwatchedContainer;
    let watchedContainer;
    let buttons;

    function buttonIsActive(button) {
        return button.classList.contains("active");
    }

    function sortMovies(container, sortMode) {
        const children = Array.from(container.children);
        for (const c of children) {
            container.removeChild(c);
        }

        children.sort((a, b) => {
            const aDate = a.dataset.dateAdded || '0000-00-00';
            const bDate = b.dataset.dateAdded || '0000-00-00';
            const aTitle = (a.dataset.title || '').toLowerCase();
            const bTitle = (b.dataset.title || '').toLowerCase();
            
            switch (sortMode) {
                case 'oldest-first':
                    if (aDate < bDate)
                        return -1;
                    else if (aDate > bDate)
                        return 1;
                    else return 0;
                case 'newest-first':
                    if (bDate < aDate)
                        return -1;
                    else if (bDate > aDate)
                        return 1;
                    else return 0;
                case 'alpha-asc':
                    if (aTitle < bTitle)
                        return -1;
                    else if (aTitle > bTitle)
                        return 1;
                    else return 0;
                case 'alpha-des':
                    if (bTitle < aTitle)
                        return -1;
                    else if (bTitle > aTitle)
                        return 1;
                    else return 0;
                default:
                    return 0;
            }
        });

        for (const c of children) {
            container.appendChild(c);
        }
    }

    function makeSortHandler(button, sortMode) {
        return (evt) => {
            if (buttonIsActive(button)) {
                evt.preventDefault();
                return;
            }

            sortMovies(unwatchedContainer, sortMode);
            sortMovies(watchedContainer, sortMode);

            for (const b of buttons) {
                if (b !== button) {
                    b.classList.remove("active");
                } else {
                    b.classList.add("active");
                }
            }
        };
    }

    function addSortButtons() {
        unwatchedContainer = document.getElementById("tabs-unwatched");
        watchedContainer = document.getElementById("tabs-watched");

        if (!unwatchedContainer || !watchedContainer)
            return;

        buttons = document.querySelectorAll(".responsive-sidebar button");

        for (const button of buttons) {
            const sortMode = button.dataset.sortMode;
            if (sortMode) {
                button.addEventListener("click", makeSortHandler(button, sortMode));
            }
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        addSortButtons();
    });
})();