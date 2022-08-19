$(function () {
    let $modal = $('#content-modal')
    let $closeButton = $($modal.find('.head').find('.close'))
    let $content = $($modal.find('#content'))
    let $questBoxes = $(".mod-contents").find('.card.q-voice')
    let $searchbar = $("#q_search")

    $searchbar.keyup(function () {
        $('#results_container').find('.card.q-voice').click(function () {
            $.getJSON(`/api/content/quest-info?apiKey=11OpnUsvX54xFG19&questId=${$(this).attr("data-q-id")}`, function (data) {
                let npcs = data[0].npcs
                let html = ``
                npcs.forEach(npc => {
                    html += `
                        <div class="npc">
                            <img class="image" src="dynamic/npcs/${npc.id}.png" alt="NPC avatar"/>
                            <p class="name"><a href="contents/npc/${npc.id}">${npc.name}</a></p>
                        </div>     
                    `
                });
                $content.html(html)
                $modal.css('display','flex')
            })
        })
    })

    $questBoxes.click(function () {
        $.getJSON(`/api/content/quest-info?questId=${$(this).attr("data-q-id")}`, function (data) {
            let npcs = data[0].npcs
            let html = ``
            npcs.forEach(npc => {
                html += `
                    <div class="npc">
                        <img class="image" src="dynamic/npcs/${npc.id}.png" alt="NPC avatar"/>
                        <p class="name"><a href="contents/npc/${npc.id}">${npc.name}</a></p>
                    </div>     
                `
            });
            $content.html(html)
            $modal.css('display','flex')
        })
    })

    $closeButton.click(function () {
        $modal.css('display','none')
    })
})