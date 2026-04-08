// Call this to register your module to main application
var moduleName = 'VirtoCommerce.SystemOperations';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider',
        function ($stateProvider) {
            $stateProvider
                .state('workspace.SystemOperationsState', {
                    url: '/system-operations',
                    templateUrl: '$(Platform)/Scripts/common/templates/home.tpl.html',
                    controller: [
                        'platformWebApp.bladeNavigationService',
                        function (bladeNavigationService) {
                            var newBlade = {
                                id: 'blade1',
                                controller: 'VirtoCommerce.SystemOperations.helloWorldController',
                                template: 'Modules/$(VirtoCommerce.SystemOperations)/Scripts/blades/hello-world.html',
                                isClosingDisabled: true,
                            };
                            bladeNavigationService.showBlade(newBlade);
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state',
        function (mainMenuService, $state) {
            //Register module in main menu
            var menuItem = {
                path: 'browse/system-operations',
                icon: 'fa fa-cube',
                title: 'SystemOperations',
                priority: 100,
                action: function () { $state.go('workspace.SystemOperationsState'); },
                permission: 'system-operations:access',
            };
            mainMenuService.addMenuItem(menuItem);
        }
    ]);
