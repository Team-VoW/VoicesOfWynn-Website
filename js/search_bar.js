function get_input() {
    var q_elements = document.getElementsByClassName('questName');
    var inpt = document.getElementById('q_search').value;
    var q_names_arr = []

    for (var i = 0; i < q_elements.length; i++) {
        q_names_arr.push(q_elements[i].innerHTML.toLowerCase());
    }
    go_to();



    function go_to() {
        let inpt_new = inpt.toLowerCase();
        if(q_names_arr.includes(inpt_new)) {
            window.find(inpt_new);
        };
    }
}


//someone please end me :D