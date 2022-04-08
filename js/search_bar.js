document.addEventListener("DOMContentLoaded",function() {
    var queryfield =  document.querySelector("#q_search")
    queryfield.addEventListener("keyup", search_quest)
    function search_quest() {
        var inpt = document.querySelector('#q_search').value
        var row_result =  document.querySelector("#results_container")
        var rows_list = document.querySelectorAll(".mod_list")
        var q_elements = []

        for(var i = 0; i< rows_list.length; i++) {
            rows_list[i].querySelectorAll(".questName").forEach(element => {
                q_elements.push(element)
            })
        }

        while(row_result.lastChild) {
            row_result.removeChild(row_result.lastChild)
        }

        if(inpt !== ""){
            var search_query = new RegExp(`${inpt.toLowerCase()}`, 'gm')
            var results = []
            q_elements.forEach(element => {
                var name = element.innerHTML.toLowerCase()
                var coll = element.parentElement.parentElement.parentElement
                if(name.match(search_query) !== null){
                    results.push(coll.cloneNode(true))
                }
            })
            for(var i = 0; i < results.length; i++){
                if(i+1%3 === 0|| i === 0){
                    var row = document.createElement("div")
                    row.classList.add("row")
                    row_result.appendChild(row)
                }
                row.appendChild(results[i])
            }
            row_result.style.display = "block"
            rows_list.forEach(row => {
                row.style.display = "none"
            })
        }else {
            row_result.style.display = "none"
            rows_list.forEach(row => {
                row.style.display = "flex"
            })
        }
    }
})