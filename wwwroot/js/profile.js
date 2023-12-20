function configurePage() {
    const profileImageFileInput = document.getElementById("uploaded-file");
    const profileImageContainer = document.querySelector('#edit-form .profile-image-container');
    const profileImageImg = document.querySelector("#edit-form .profile-image-container img");
    const editForm = document.getElementById("edit-form");
    
    if (profileImageImg && profileImageFileInput && profileImageContainer) {
        profileImageFileInput.addEventListener("change", (evt) => {
           const file = profileImageFileInput.files[0];
           if (file) {
               const reader = new FileReader();
               reader.addEventListener("load", (readerEvent) => {
                   profileImageImg.setAttribute("src", readerEvent.target.result);
               });
               reader.readAsDataURL(file);
           }
        });

        profileImageContainer.addEventListener("dragover", (evt) => {
            evt.preventDefault();
        });

        profileImageContainer.addEventListener("drop", (evt) => {
            evt.preventDefault();
            const files = evt.dataTransfer.files;
            if (files) {
                const file = files[0];
                if (file) {
                    const reader = new FileReader();
                    reader.addEventListener("load", (readerEvent) => {
                        profileImageImg.setAttribute("src", readerEvent.target.result);
                        profileImageFileInput.files = files;
                    });
                    reader.readAsDataURL(file);
                }
            }
        });
    }
    
    if (editForm) {
        editForm.addEventListener("submit", async (evt) => {
           const form = evt.target;
           editForm.checkValidity();
           form.classList.add('was-validated');
           if (form.checkValidity() === false)
           {
               evt.preventDefault();
               evt.stopPropagation();
           }
        });
    }
}

(() => {
    document.addEventListener("DOMContentLoaded", () => {
        configurePage();
    });
})();