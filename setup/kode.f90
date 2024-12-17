
module kode
    implicit none
    public :: greet
contains
    subroutine greet() bind(C, name='greet')
        PRINT *, "Hello, World!"
    end subroutine greet
end module kode

program installer
    implicit none
    PRINT *, "Welcome to the official KodeRunner setup wizard!"
    ! ! Declare the external C function
    ! interface
    !     subroutine greet() bind(C, name='greet')
    !     use iso_c_binding
    !     end subroutine greet
    ! end interface
    
    ! ! Call the C function
    ! call greet()
    
end program installer