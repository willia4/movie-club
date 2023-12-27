
(() => {

    /** @function 
     * @param { HTMLElement } el */
    function disableElement(el) { el.setAttribute('disabled', 'disabled')}

    /** @function
     * @param { HTMLElement } el */
    function enableElement(el) { el.removeAttribute('disabled'); }
    
    function configurePage() {
        const setWatchDateButton = document.getElementById("set-watch-date-button");
        const deleteWatchDateButton = document.getElementById("clear-watch-dates");
        
        const modalElement = document.getElementById('set-watch-date-modal');
        const modalDateElement = document.getElementById('set-watch-date-input');
        
        /** @function
         * 
         * @param {HTMLElement} el
         * @returns boolean
         */
        function inputElementHasValidDate(el) {
            return !!el.value;
        }
        
        if (deleteWatchDateButton) {
            const movieId = deleteWatchDateButton.dataset.movieid;
            deleteWatchDateButton.addEventListener('click', async (evt) => {
                const getRes = await fetch(`/api/v1/movies/${movieId}/watch-dates`, { method: 'GET'});
                const body = JSON.parse(await getRes.text());
                const count = body.allWatchDates.length;
                const ans = confirm(`Delete ${count} watch dates on this movie?`);
                
                if (ans !== true) { return; }
                
                const deleteRes = await fetch(`/api/v1/movies/${movieId}/watch-dates`, { method: 'DELETE'});
                if (!deleteRes.ok) {
                    alert ("Error");
                    console.log(deleteRes);
                }
                else {
                    location.reload();
                }
            })
            
            
        } 
        if (setWatchDateButton && modalElement && modalDateElement) {
            setWatchDateButton.addEventListener("click", (evt) => {
               evt.preventDefault();
               evt.stopPropagation();

               const modal = new bootstrap.Modal(modalElement, {
                   'backdrop': 'static',
                   'keyboard': false,
                   'focus': true
               });

               const dateParts =
                   new Intl.DateTimeFormat(
                       'en', 
                       {
                           year: 'numeric',
                           day: '2-digit',
                           month: '2-digit'
                        })
                       .formatToParts()
                       .reduce((acc, next) => {
                           if (next.type !== 'literal') {
                               acc[next.type] = next.value;
                           }
                           return acc;
                       }, {});
               
               modalDateElement.value = `${dateParts.year}-${dateParts.month}-${dateParts.day}`;
               
               modal.show();
            });
        }
        
        if (modalElement && modalDateElement) {
            ((modalElement, modalDateElement) => {
                const movieId = setWatchDateButton.dataset.movieid;
                /** @type HTMLElement[] */
                const saveButtons = Array.from(modalElement.querySelectorAll(".btn.btn-primary"));
                /** @type HTMLElement[] */
                const cancelButtons = Array.from(modalElement.querySelectorAll(".btn.btn-secondary"));
                
                saveButtons.forEach(btn => {
                   btn.addEventListener("click", async evt => {
                       evt.preventDefault();
                       evt.stopPropagation();
                       
                       const modal = bootstrap.Modal.getInstance(modalElement);
                       if (!modal) return;

                       if (!inputElementHasValidDate(modalDateElement)) {
                           alert("Date is required");
                           return;
                       }
                       
                       saveButtons.forEach(b => {
                           b.classList.add("loading");
                           b.classList.remove("not-loading");
                       })

                       const disabledElements = saveButtons.concat(cancelButtons).concat([modalDateElement]);
                       disabledElements.forEach(disableElement);
                       
                       try {
                           const res = await fetch(`/api/v1/movies/${movieId}/watch-dates`, {
                               method: "POST", 
                               body: JSON.stringify({
                                   newWatchDate: modalDateElement.value
                               }), 
                               headers: {
                                   "Content-Type": "application/json"
                               }
                           });
                           
                           if (!res.ok) {
                               console.log(res.status);
                               alert(`Could not set date`);
                           }
                           else {
                               const resBody = JSON.parse(await res.text());
                               if (resBody.mostRecentWatchDate) {
                                   document.querySelector("span.watch-date-value").innerText = resBody.mostRecentWatchDate;
                               }
                               modal.hide();
                           }
                       }
                       finally {
                           saveButtons.forEach(b => {
                               b.classList.remove("loading");
                               b.classList.add("not-loading");
                           })
                           
                           disabledElements.forEach(enableElement);
                       }
                   }) ;
                });
                
                cancelButtons.forEach(btn => {
                    btn.addEventListener("click", evt => {
                       evt.preventDefault();
                       evt.stopPropagation();

                        const modal = bootstrap.Modal.getInstance(modalElement);
                        if (!modal) return;

                        modal.hide();
                    });
                });

                const handleInputChange = (evt) => {
                    const notLoadingSaveButtons = saveButtons.filter(btn => !btn.classList.contains('loading'));
                    
                    if (inputElementHasValidDate(evt.target)) {
                        notLoadingSaveButtons.forEach(btn => {btn.removeAttribute('disabled'); });
                    } else {
                        notLoadingSaveButtons.forEach(btn => {btn.setAttribute('disabled', 'disabled'); });
                    }
                };

                modalDateElement.addEventListener('input', handleInputChange);
                modalDateElement.addEventListener('change', handleInputChange);
            })(modalElement, modalDateElement);
        }
    }
    
    document.addEventListener("DOMContentLoaded", () => {
        configurePage();
    });
})();