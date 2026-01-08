document.addEventListener("DOMContentLoaded", function () {
    var queryfield = document.querySelector("#q_search");
    if (!queryfield) return;

    var resultsContainer = document.querySelector("#results_container");
    if (!resultsContainer) return;
    var questsGrid = document.querySelectorAll(".quests-grid:not(#results_container)");
    var allQuestCards = document.querySelectorAll(".quests-grid:not(#results_container) .quest-card");
    queryfield.addEventListener("keyup", search_quest);

    function search_quest() {
        var input = queryfield.value.toLowerCase().trim();

        // Clear previous results
        while (resultsContainer.lastChild) {
            resultsContainer.removeChild(resultsContainer.lastChild);
        }

        if (input !== "") {
            var searchRegex = new RegExp(input, 'gi');
            var results = [];

            allQuestCards.forEach(function (card) {
                var questName = card.querySelector(".quest-card__name");
                if (questName && questName.textContent.toLowerCase().match(searchRegex)) {
                    var clone = card.cloneNode(true);
                    // Ensure cloned cards are visible (bypass scroll animation)
                    clone.classList.add('is-visible');
                    results.push(clone);
                }
            });

            // Add cloned results to results container
            results.forEach(function (card) {
                resultsContainer.appendChild(card);
            });

            // Show results, hide main grid
            resultsContainer.style.display = "grid";
            questsGrid.forEach(function (grid) {
                grid.style.display = "none";
            });
        } else {
            // No search query, show main grids
            resultsContainer.style.display = "none";
            questsGrid.forEach(function (grid) {
                grid.style.display = "grid";
            });
        }
    }

    // Initialize scroll animations for quest cards
    initScrollAnimations();
});

// Intersection Observer for scroll animations
function initScrollAnimations() {
    var animatedElements = document.querySelectorAll('.animate-on-scroll');

    // Check if user prefers reduced motion
    var prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (prefersReducedMotion) {
        animatedElements.forEach(function (el) {
            el.classList.add('is-visible');
        });
        return;
    }

    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            }
        });
    }, {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    });

    animatedElements.forEach(function (el) {
        observer.observe(el);
    });
}