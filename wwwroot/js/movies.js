(() => {
    
    async function search() {
        const res = await fetch("/api/v1/search", {
           method: "POST", 
           mode: "no-cors",
           body: JSON.stringify({
               
           }),
            headers: {
               "Content-Type": "application/json"
            }
        });
        
        if (!res.ok)
        {
            throw new Error("Invalid response from server: " + res.status);
        }
        
        const body = await res.text();
        return body;
    }
    document.addEventListener("DOMContentLoaded", () => {
       let searchButton = document.getElementById("search");
       searchButton.addEventListener("click", async () => {
           let res = await search();
       })
    });
})();